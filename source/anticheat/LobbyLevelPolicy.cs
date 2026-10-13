using System;
using System.Collections.Generic;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace AUAnticheat.anticheat
{
	// Host admission preference, not a cheat detector. Levels are not proof of
	// account age or authenticity and this is not a reliable anti-raid boundary.
	internal static class LobbyLevelPolicy
	{
		public static bool Enabled { get; set; } = false;
		public static uint MinimumLevel { get; set; } = 10;
		private sealed class Observation
		{
			public uint NetId, Level;
			public bool RemovalRequested;
			public float StableSince;
		}
		private static readonly Dictionary<int, Observation> Players = new();
		private sealed class LevelRecord
		{
			public PlayerControl Player;
			public uint NetId;
			public uint? LastSet;
			public uint? LastLive;
			public bool SawNonzero;
			public string Diagnostic;
		}
		private static readonly Dictionary<int, LevelRecord> Levels = new();
		private static string levelRoom = "";
		private static LevelRecord Record(PlayerControl player)
		{
			string room = AmongUsClient.Instance == null ? "" : $"{AmongUsClient.Instance.GameId}:{AmongUsClient.Instance.HostId}";
			if(room != levelRoom) { Levels.Clear(); levelRoom = room; }
			if(!Levels.TryGetValue(player.OwnerId, out var record) || record.Player != player || record.NetId != player.NetId)
			{
				if(Levels.Count >= 64) Levels.Clear();
				Levels[player.OwnerId] = record = new LevelRecord { Player = player, NetId = player.NetId };
			}
			return record;
		}
		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.SetLevel))]
		private static class ObserveAppliedLevel
		{
			private static void Postfix(PlayerControl __instance, uint __0)
			{
				if(__instance == null || __0 == uint.MaxValue) return;
				var record = Record(__instance);
				record.LastSet = __0;
				if(__0 > 0) record.SawNonzero = true;
			}
		}
		internal static bool TryLevel(PlayerControl player, ClientData peer, out uint level, out string source)
		{
			level = uint.MaxValue; source = "waiting for complete player data";
			var record = Record(player);
			uint live = player.Data.PlayerLevel, joined = peer.PlayerLevel;
			if((live > 0 && live != uint.MaxValue) || (joined > 0 && joined != uint.MaxValue)) record.SawNonzero = true;
			if(player.Data.IsIncomplete) return false;
			// Live data is the same record used by the game's UI and other level
			// filters. A stale join snapshot must not permanently disable admission.
			if(live > 0 && live != uint.MaxValue) { record.LastLive = live; level = live; source = "live player record"; return true; }
			if(joined > 0 && joined != uint.MaxValue)
			{
				if((record.LastLive.HasValue && record.LastLive.Value != joined) ||
					(record.LastSet.HasValue && record.LastSet.Value > 0 && record.LastSet.Value != joined))
				{ source = "join snapshot conflicts with the last live level; waiting for live data"; return false; }
				level = joined; source = "current join record (live level unavailable)"; return true;
			}
			// Default zero and an explicit level-one update look identical in memory.
			// Require an observed SetLevel(0), complete data and no higher level seen
			// on this same player object. Never convert a timeout alone into evidence.
			if(live == 0 && joined == 0 && record.LastSet == 0 && !record.SawNonzero)
			{ level = 0; source = "observed level-one update"; return true; }
			source = record.SawNonzero ? "zero conflicts with an earlier level on this player object" : "level 1 is unconfirmed; waiting for a level update";
			if(record.Diagnostic != source)
			{
				record.Diagnostic = source;
				AUAnticheatPlugin.Log.LogWarning($"[Level filter] Lobby ID {player.OwnerId}: {source}. No level-based ban requested.");
			}
			return false;
		}
		private static string session = "";
		private static float nextScan;
		private static float nextRemoval, pendingSince;
		private static int pendingOwner = -1;
		private static uint pendingNetId;
		private static string pendingSession = "";
		public static void Forget(int ownerId) { Players.Remove(ownerId); Levels.Remove(ownerId); }
		// Keep the send cooldown across toggles/threshold edits; otherwise toggling
		// the rule could turn a paced policy back into a removal burst.
		public static void Reset() { ClearObservations(); Levels.Clear(); levelRoom = ""; pendingOwner = -1; pendingSession = ""; }
		private static void ClearObservations() { Players.Clear(); session = ""; nextScan = 0; }

		[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.FixedUpdate))]
		private static class ObserveLobby
		{
			private static void Postfix()
			{
				var client = AmongUsClient.Instance;
				bool waiting = LobbyBehaviour.Instance != null && client != null &&
					client.GameState == InnerNetClient.GameStates.Joined &&
					(GameManager.Instance == null || !GameManager.Instance.GameHasStarted);
					if((!Enabled && !ModerationStore.EnforceBans) || !AUAnticheatPlugin.ProtectionsAttached || !waiting || !client.AmHost)
				{ ClearObservations(); return; }
				try
				{
					string current = $"{client.GameId}:{client.HostId}:{LobbyBehaviour.Instance.GetInstanceID()}:{MinimumLevel}";
					if(current != session) { ClearObservations(); session = current; nextRemoval = Math.Max(nextRemoval, Time.realtimeSinceStartup + 2); }
					float now = Time.realtimeSinceStartup;
					string room = $"{client.GameId}:{client.HostId}:{LobbyBehaviour.Instance.GetInstanceID()}";
					if(room != pendingSession) { pendingOwner = -1; pendingSession = room; }
					if(now < nextScan) return;
					nextScan = now + 0.1f;
					if(pendingOwner >= 0)
					{
						var pending = client.FindClientById(pendingOwner)?.Character;
						if(pending == null || pending.NetId != pendingNetId || pending.Data == null || pending.Data.Disconnected)
						{ pendingOwner = -1; nextRemoval = Math.Max(nextRemoval, now + 2); }
						else
						{
							if(now - pendingSince >= 15)
							{
								Enabled = false;
								ModerationStore.EnforceBans = false;
								AUAnticheatPlugin.notifications.Send("Lobby level rule", "Entry rule paused: a previous ban has not completed. No more bans will be sent. Check the lobby before enabling it again.");
							}
							return;
						}
					}
					if(now < nextRemoval) return;
					// Respect vanilla moderation availability. Never send a ban while
					// local host identity is unsettled or the game says banning is unavailable.
					if(client.ClientId != client.HostId || !client.CanBan()) return;
					foreach(var player in PlayerControl.AllPlayerControls)
					{
						if(player == null || player == PlayerControl.LocalPlayer || player.OwnerId == client.HostId || player.Data == null || player.Data.Disconnected ||
							client.FindClientById(player.OwnerId)?.Character != player) continue;
						uint level = player.Data.PlayerLevel;
						var peer = client.FindClientById(player.OwnerId);
						// Bind identity, but resolve the level separately: join snapshots
						// may lag behind the live record used by the game's UI.
						bool identityReady = peer != null && !peer.IsBeingCreated &&
							FriendCodeExceptions.Normalize(peer.FriendCode) != null &&
							FriendCodeExceptions.Normalize(peer.FriendCode) == FriendCodeExceptions.Normalize(player.Data.FriendCode);
						bool persistentBan = identityReady && ModerationStore.EnforceBans && ModerationStore.Contains(peer.FriendCode,"ban");
						string levelSource = "waiting for player identity";
						bool ready = identityReady && TryLevel(player, peer, out level, out levelSource);
						if(!persistentBan && !Enabled) continue;
						if(!ready && !persistentBan) { Players.Remove(player.OwnerId); continue; }
						if(!Players.TryGetValue(player.OwnerId, out var observed) || observed.NetId != player.NetId || observed.Level != level)
						{
							observed = new Observation { NetId = player.NetId, Level = level, StableSince = now };
							Players[player.OwnerId] = observed;
						}
						// TryLevel already requires an explicit level-one update and complete
						// data. Add a settling interval, not a timeout-based assumption.
						if(!persistentBan && level == 0 && now - observed.StableSince < 5f) continue;
						if(observed.RemovalRequested || (!persistentBan && !LobbyAdmissionRules.ShouldBan(Enabled, client.AmHost, waiting, false, ready,
							FriendCodeExceptions.Contains(peer.FriendCode), level, MinimumLevel))) continue;
						if(peer.Id != player.OwnerId || client.GetClientIdFromCharacter(player) != peer.Id || peer.Id == client.ClientId) continue;
						client.KickPlayer(player.OwnerId, true);
						if(!persistentBan) AUAnticheatPlugin.Log.LogWarning($"[Level filter] Ban requested: lobby ID {player.OwnerId}; displayed level {(ulong)level + 1}; minimum {MinimumLevel}; source={levelSource}; liveRaw={player.Data.PlayerLevel}; joinRaw={peer.PlayerLevel}. Entry policy, not cheating evidence.");
						pendingOwner = player.OwnerId; pendingNetId = player.NetId; pendingSince = now;
						nextRemoval = now + 2;
						observed.RemovalRequested = true;
						AUAnticheatPlugin.notifications.Send(persistentBan ? "Persistent ban" : "Lobby level rule",
							persistentBan ? $"Ban requested for {PublicText.Name(player.Data.PlayerName)}: matching your saved local ban list. Not a new cheating accusation." :
							$"Ban requested for {PublicText.Name(player.Data.PlayerName)}: level {(ulong)level + 1}, below this lobby's minimum of {MinimumLevel}. This is an entry rule, not a cheating accusation.");
						break; // One request, then wait for departure and cooldown. Never batch-ban a full lobby.
					}
				}
				catch(Exception exception)
				{
					Enabled = false;
					ModerationStore.EnforceBans = false;
					Reset();
					AUAnticheatPlugin.Log.LogWarning($"Disabled lobby level rule after {exception.GetType().Name}.");
					AUAnticheatPlugin.notifications.Send("Lobby level rule", "Level rule disabled after an error. Check the log before enabling it again.");
				}
			}
		}
	}
}
