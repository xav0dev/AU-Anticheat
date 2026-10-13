using HarmonyLib;
using AmongUs.GameOptions;
using UnityEngine;

namespace AUAnticheat.anticheat
{
	internal static class RoundReports
	{
		public static bool Enabled = true;
		private static RoundLedger ledger = new();
		private static int gameId, hostId;
		private static bool queued;
		private static float waitingSince = -1;
		private static string readyLobby = "";
		internal const float ReturnDelay = 5f;
		private static string ReadyKey => LobbyChat.WaitingLobby ?
			$"{AmongUsClient.Instance.GameId}:{AmongUsClient.Instance.HostId}:{LobbyBehaviour.Instance.GetInstanceID()}:{PlayerControl.LocalPlayer.NetId}" : "";
		internal static bool ReadyToSend => Enabled && ledger.Completed && readyLobby.Length > 0 && readyLobby == ReadyKey &&
			waitingSince >= 0 && Time.realtimeSinceStartup - waitingSince >= ReturnDelay;
		[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnDisconnected))]
		private static class ClearLobbyServices
		{
			private static void Postfix()
			{
				ledger = new(); queued = false; waitingSince = -1; readyLobby = "";
				LobbyChat.Clear(); LobbyRules.Disconnect(); ActivityTracker.Reset(); LobbyExtension.Reset(); LobbyLevelPolicy.Reset();
			}
		}
		public static void Begin()
		{
			if(AmongUsClient.Instance == null) return;
			if(ledger.Running && gameId == AmongUsClient.Instance.GameId && hostId == AmongUsClient.Instance.HostId) return;
			LobbyRules.NewRound();
			gameId = AmongUsClient.Instance.GameId; hostId = AmongUsClient.Instance.HostId;
			queued = false; waitingSince = -1; readyLobby = ""; LobbyChat.Clear();
			ledger.Begin(Time.realtimeSinceStartup); Snapshot();
		}
		private static void Observe(PlayerControl player)
		{
			if(player == null || player.Data == null) return;
			var data = player.Data;
			var role = data.RoleType;
			if(RoleManager.IsGhostRole(role) && data.RoleWhenAlive.HasValue) role = data.RoleWhenAlive.Value;
			if(RoleManager.IsGhostRole(role)) return;
			ledger.Observe(player.NetId, data.PlayerName, role.ToString(), RoleManager.IsImpostorRole(role));
		}
		private static void Snapshot() { foreach(var player in PlayerControl.AllPlayerControls) Observe(player); }
		[HarmonyPatch(typeof(RoleManager), nameof(RoleManager.SetRole))]
		private static class CaptureAssignedRole
		{
			private static void Postfix(PlayerControl __0, RoleTypes __1)
			{
				if(!ledger.Running || __0?.Data == null || RoleManager.IsGhostRole(__1)) return;
				ledger.Observe(__0.NetId, __0.Data.PlayerName, __1.ToString(), RoleManager.IsImpostorRole(__1));
			}
		}
		[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnPlayerLeft))]
		private static class PreserveLeavingRole
		{
			private static void Prefix(InnerNet.ClientData __0) { Observe(__0?.Character); }
		}
		public static void Tick()
		{
			var client = AmongUsClient.Instance;
			if(client == null || client.GameId != gameId || client.HostId != hostId) { ledger = new(); waitingSince = -1; readyLobby = ""; return; }
			if(ledger.Running) { Snapshot(); return; }
			string current = Enabled && ledger.Completed ? ReadyKey : "";
			if(current.Length == 0) { waitingSince = -1; readyLobby = ""; return; }
			if(current != readyLobby) { readyLobby = current; waitingSince = Time.realtimeSinceStartup; }
			if(queued || !ReadyToSend) return;
			var lines = ledger.Lines();
            int bodyBudget = LobbyChat.MaximumPublicMessageLength - LobbyChat.CombinedMessage(new[]{"x"}).Length + 1;
            string summary = ledger.ChatSummary(bodyBudget);
            if(summary == null)
			{
				queued = true; // One local notice, never repeated attempts or a split public report.
                AUAnticheatPlugin.notifications.Send("Match results", "Results kept locally: impostor data was unavailable or the full roster could not fit one message. No roles were guessed. Kills are observed by this client.\n" + string.Join("\n", lines));
				return;
			}
            queued = LobbyChat.QueueReport(new[]{summary});
            if(queued) AUAnticheatPlugin.notifications.Send("Match results", "Full results (observed kills):\n" + string.Join("\n", lines));
		}
		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
		private static class OnKillResult
		{
			private static void Prefix(PlayerControl __0, MurderResultFlags __1, out bool __state)
			{
				__state = __0 != null && __0.Data != null && !__0.Data.IsDead && (__1 & MurderResultFlags.Succeeded) != 0;
			}
			private static void Postfix(PlayerControl __instance, PlayerControl __0, bool __state)
			{
				if(!__state || __instance == null || __0 == null) return;
				Observe(__instance); ledger.Kill(__instance.NetId, __0.NetId);
			}
		}
		[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
		private static class OnEnd
		{
			private static void Prefix(EndGameResult __0)
			{
				if(__0 == null) return;
				Snapshot(); ledger.End(Time.realtimeSinceStartup, (int)__0.GameOverReason);
			}
		}
	}
}
