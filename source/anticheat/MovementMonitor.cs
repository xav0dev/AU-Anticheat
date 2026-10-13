using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace AUAnticheat.anticheat
{
	internal static class MovementMonitor
	{
		private sealed class MovementSample
		{
			public Vector2 Position;
			public DateTime TimeUtc;
			public DateTime SpeedWindowStartedUtc;
			public float PathDistance;
			public int ConsecutiveSpeedWindows;
			public int ConsecutiveLargeJumps;
			public DateTime LargeJumpSequenceStartedUtc;
		}

		private static readonly Dictionary<int, MovementSample> Samples = new Dictionary<int, MovementSample>();
		private static string sessionKey = string.Empty;
		public static void Reset() { Samples.Clear(); sessionKey = string.Empty; }
		public static void Forget(int ownerId) { Samples.Remove(ownerId); }

		[HarmonyPatch(typeof(CustomNetworkTransform), nameof(CustomNetworkTransform.Deserialize))]
		private static class OnMovementData
		{
			private static void Postfix(CustomNetworkTransform __instance)
			{
				if(!Anticheat.Enabled || __instance == null) return;

				PlayerControl player = __instance.myPlayer;
				if(player == null || player == PlayerControl.LocalPlayer || player.Data == null) return;
				if(GameManager.Instance == null || !GameManager.Instance.GameHasStarted || LobbyBehaviour.Instance != null) return;

				ResetForNewSession();
				using var evidence = SenderEvidence.Begin("Movement observation", null, player.NetId, player.OwnerId, PublicText.Name(player.Data.PlayerName));
				Vector2 position = player.GetTruePosition();
				DateTime now = DateTime.UtcNow;

				if(!Samples.TryGetValue(player.OwnerId, out MovementSample sample))
				{
					Samples[player.OwnerId] = new MovementSample { Position = position, TimeUtc = now, SpeedWindowStartedUtc = now };
					return;
				}

				Vector2 previousPosition = sample.Position;
				double elapsed = (now - sample.TimeUtc).TotalSeconds;
				float distance = Vector2.Distance(previousPosition, position);
				sample.Position = position;
				sample.TimeUtc = now;

				if(elapsed < 0.03 || elapsed > 2 || !MovementContext.StableLivingPlayer(player))
				{
					ResetSpeedWindow(sample, now);
					sample.ConsecutiveLargeJumps = 0;
					return;
				}

				float expectedSpeed = player.MyPhysics != null ? player.MyPhysics.TrueSpeed : 3.0f;
				float allowedDistance = Math.Max(3.5f, expectedSpeed * (float)elapsed * 5.0f + 2.0f);
				if(distance > allowedDistance)
				{
					// One correction is ambiguous: kills, transitions and network catch-up can
					// all create a single large deserialize step. Exclude it from sustained
					// speed accounting and require correlation unless it is a huge teleport.
					if(sample.ConsecutiveLargeJumps == 0) sample.LargeJumpSequenceStartedUtc = now;
					sample.ConsecutiveLargeJumps++;
					if(sample.ConsecutiveLargeJumps >= 3)
					{
						double sequenceSeconds = (now - sample.LargeJumpSequenceStartedUtc).TotalSeconds;
						// A burst shorter than this is normally buffered network state being
						// replayed after lag, not three independently observed movements.
						if(sequenceSeconds >= 0.35)
						{
							bool extremeSequence = distance > 18.0f && sequenceSeconds >= 0.75;
							Anticheat.Flag(player, $"{player.Data.PlayerName} produced repeated abnormal movement over {sequenceSeconds:F2}s ({distance:F1} units in the latest step; safe step {allowedDistance:F1}).", extremeSequence);
						}
						sample.ConsecutiveLargeJumps = 0;
						sample.LargeJumpSequenceStartedUtc = DateTime.MinValue;
					}

					ResetSpeedWindow(sample, now, false);
					return;
				}

				sample.ConsecutiveLargeJumps = 0;
				sample.LargeJumpSequenceStartedUtc = DateTime.MinValue;
				sample.PathDistance += distance;
				CheckSustainedSpeed(player, sample, now, expectedSpeed);
			}
		}

		private static void CheckSustainedSpeed(PlayerControl player, MovementSample sample, DateTime now, float expectedSpeed)
		{
			double windowSeconds = (now - sample.SpeedWindowStartedUtc).TotalSeconds;
			if(windowSeconds < 1.0) return;
			if(windowSeconds > 3.0)
			{
				ResetSpeedWindow(sample, now);
				return;
			}

			float allowedPath = expectedSpeed * (float)windowSeconds * 1.45f + 0.75f;
			if(sample.PathDistance > allowedPath)
			{
				float ratio = allowedPath > 0 ? sample.PathDistance / allowedPath : float.MaxValue;
				sample.ConsecutiveSpeedWindows++;
				bool extreme = ratio >= 2.4f;
				if(extreme || sample.ConsecutiveSpeedWindows >= 2)
				{
					Anticheat.Flag(player, $"{player.Data.PlayerName} sustained {sample.PathDistance / (float)windowSeconds:F1} units/s (safe path limit {allowedPath / (float)windowSeconds:F1}).", extreme);
					sample.ConsecutiveSpeedWindows = 0;
				}
			}
			else
			{
				sample.ConsecutiveSpeedWindows = 0;
			}

			ResetSpeedWindow(sample, now, false);
		}

		private static void ResetSpeedWindow(MovementSample sample, DateTime now, bool resetConsecutive = true)
		{
			sample.SpeedWindowStartedUtc = now;
			sample.PathDistance = 0;
			if(resetConsecutive) sample.ConsecutiveSpeedWindows = 0;
		}

		private static void ResetForNewSession()
		{
			string current = AmongUsClient.Instance == null ? "offline" : $"{AmongUsClient.Instance.GameId}:{AmongUsClient.Instance.HostId}";
			if(current == sessionKey) return;

			Samples.Clear();
			sessionKey = current;
		}
	}
}
