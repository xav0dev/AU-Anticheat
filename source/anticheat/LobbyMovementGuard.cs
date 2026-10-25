using System;
using System.Collections.Generic;
using HarmonyLib;
using Hazel;
using UnityEngine;

namespace AUAnticheat.anticheat
{
	// Inspect explicit lobby teleport RPCs only. Interpolated walking is not a path trace.
	// This deliberately does not treat a wall crossing as evidence against an actor.
	internal static class LobbyMovementGuard
	{
		public static bool Enabled = true;
		public static int BlockedSnaps { get; private set; }
		private sealed class Sample
		{
			public uint NetId;
			public float ReadyAt, ObservedAt, SavedAt;
			public bool HasSafe;
			public Vector2 Feet;
		}
		private enum PathState { Clear, Touching, Crossed, Unknown }
		private static readonly Dictionary<int, Sample> Samples = new();
		private static string session = "";
		private static float lastNotice = -100;
		private static float nextScan;
		public static void Forget(int ownerId) => Samples.Remove(ownerId);
		public static void Reset()
		{
			Samples.Clear(); session = ""; lastNotice = -100; nextScan = 0;
			BlockedSnaps = 0;
		}
		private static bool Active()
		{
			return Enabled && AUAnticheatPlugin.ProtectionsAttached && LobbyBehaviour.Instance != null &&
				AmongUsClient.Instance != null && AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Joined &&
				(GameManager.Instance == null || !GameManager.Instance.GameHasStarted);
		}
		private static void EnsureSession()
		{
			string key = $"{AmongUsClient.Instance.GameId}:{AmongUsClient.Instance.HostId}:{LobbyBehaviour.Instance.GetInstanceID()}";
			if(key != session) { Reset(); session = key; }
		}
		private static Sample GetSample(PlayerControl player, float now)
		{
			if(!Samples.TryGetValue(player.OwnerId, out Sample sample) || sample.NetId != player.NetId)
			{
				sample = new Sample { NetId = player.NetId, ReadyAt = now + MovementRecoveryRules.JoinGraceSeconds, ObservedAt = now };
				Samples[player.OwnerId] = sample;
			}
			return sample;
		}
		private static bool Stable(PlayerControl player)
		{
			return player != null && player.Data != null && !player.Data.IsDead && player.NetTransform != null &&
				player.MyPhysics != null && !player.MyPhysics.DoingCustomAnimation && !player.inVent && !player.onLadder && !player.inMovingPlat;
		}
		private static bool Solid(Collider2D collider)
		{
			return collider != null && collider.enabled && !collider.isTrigger &&
				(collider.attachedRigidbody == null || collider.attachedRigidbody.bodyType == RigidbodyType2D.Static);
		}
		private static bool ClearPoint(Vector2 point)
		{
			if(!MovementRecoveryRules.Finite(point.x, point.y) || Constants.ShipOnlyMask == 0) return false;
			var hits = Physics2D.OverlapCircleAll(point, 0.06f, Constants.ShipOnlyMask);
			if(hits.Length > 64) return false;
			foreach(var hit in hits) if(Solid(hit)) return false;
			return true;
		}
		private static PathState Trace(Vector2 start, Vector2 end)
		{
			if(!MovementRecoveryRules.Finite(end.x, end.y) || !ClearPoint(start) || !ClearPoint(end)) return PathState.Unknown;
			float distance = Vector2.Distance(start, end);
			if(distance < 0.01f) return PathState.Clear;
			var hits = Physics2D.LinecastAll(start, end, Constants.ShipOnlyMask);
			if(hits.Length > 64) return PathState.Unknown;
			bool touched = false;
			foreach(var hit in hits)
			{
				if(!Solid(hit.collider)) continue;
				if(hit.fraction <= 0) return PathState.Unknown;
				if(MovementRecoveryRules.Crossed(distance, hit.distance)) return PathState.Crossed;
				touched = true;
			}
			return touched ? PathState.Touching : PathState.Clear;
		}
		private static void Save(Sample sample, Vector2 feet, float now)
		{
			// Wall contact invalidates the old sample: retaining it turns walking
			// around furniture into an imaginary straight-line wall crossing.
			sample.HasSafe = ClearPoint(feet);
			if(!sample.HasSafe) return;
			sample.Feet = feet; sample.SavedAt = now;
		}
		private static void Notice(string text, float now)
		{
			if(now - lastNotice < 2) return;
			lastNotice = now;
			AUAnticheatPlugin.notifications.Send("Movement protection", text + " Sender unverified; no points or ban.");
		}
		[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.FixedUpdate))]
		private static class ObserveLobby
		{
			private static void Postfix()
			{
				if(!Active()) { Samples.Clear(); return; }
				try
				{
					EnsureSession();
					float now = Time.realtimeSinceStartup;
					if(now < nextScan) return;
					nextScan = now + 0.05f;
					foreach(PlayerControl player in PlayerControl.AllPlayerControls)
					{
						if(player == null || player.Data == null) continue;
						Sample sample = GetSample(player, now);
						float elapsed = now - sample.ObservedAt;
						sample.ObservedAt = now;
						if(!Stable(player)) { sample.HasSafe = false; sample.ReadyAt = now + 1; continue; }
						if(now < sample.ReadyAt) continue;
						Vector2 feet = player.GetTruePosition();
						if(!MovementRecoveryRules.Fresh(elapsed)) { sample.HasSafe = false; sample.ReadyAt = now + 1; continue; }
						// Never SnapTo/RpcSnapTo based on ordinary observed motion. A chord
						// between network samples cannot reconstruct the player's real path.
						Save(sample, feet, now);
					}
				}
				catch(Exception e)
				{
					Enabled = false; Samples.Clear();
					AUAnticheatPlugin.Log.LogWarning($"Lobby movement guard disabled after {e.GetType().Name}: {e.Message}");
					AUAnticheatPlugin.notifications.Send("Movement protection", "Lobby movement guard disabled after an error. Other protections remain enabled.");
				}
			}
		}
		[HarmonyPatch(typeof(CustomNetworkTransform), nameof(CustomNetworkTransform.HandleRpc))]
		private static class BeforeLobbySnap
		{
			[HarmonyPriority(Priority.First)]
			private static bool Prefix(CustomNetworkTransform __instance, byte __0, MessageReader __1)
			{
				if(__0 != (byte)RpcCalls.SnapTo || !Active() || __instance == null) return true;
				PlayerControl player = __instance.myPlayer;
				if(!Stable(player)) return true;
				using var evidence = SenderEvidence.Begin("Lobby teleport", __0, player.NetId, player.OwnerId, PublicText.Name(player.Data.PlayerName));
				EnsureSession();
				float now = Time.realtimeSinceStartup;
				Sample sample = GetSample(player, now);
				if(!sample.HasSafe || now < sample.ReadyAt || !MovementRecoveryRules.Fresh(now - sample.SavedAt)) return true;
				int oldPosition = __1.Position;
				try
				{
					Vector2 destination = NetHelpers.ReadVector2(__1);
					__1.ReadUInt16();
					Vector2 feetOffset = player.GetTruePosition() - (Vector2)player.transform.position;
					try
					{
						if(!SceneBoundaryMonitor.OutsideLobby(destination+feetOffset) && Trace(sample.Feet, destination + feetOffset) != PathState.Crossed) return true;
					}
					catch(Exception) { sample.HasSafe = false; return true; } // Geometry failure is not a malformed packet.
					BlockedSnaps++;
					SenderEvidence.Record();
					Notice($"Affected: {PublicText.Name(player.Data.PlayerName)}. Blocked a lobby teleport across a wall or outside the loaded lobby bounds before it applied locally.", now);
					return false;
				}
				catch(Exception) { return PacketPreflight.Block("invalid lobby teleport payload"); }
				finally { __1.Position = oldPosition; }
			}
		}
	}
}
