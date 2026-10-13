using HarmonyLib;
using UnityEngine;

namespace AUAnticheat.anticheat
{
	internal static class LobbyExtension
	{
		public static bool Enabled = true;
		private static int lobbyInstance, offeredId, requestedId = -1, host, game;
		private static float expires;
		private static float nextAllowedRequest;
		private static bool available;
		private static bool awaitingConfirmation;
		private static float requestedAt;
		public static string Status { get; private set; } = "Waiting for the server to offer an extension. Inactivity closure is not overridden.";
		public static void Reset() { available = false; awaitingConfirmation = false; requestedId = -1; nextAllowedRequest = 0; Status = "Waiting for the server to offer an extension. Inactivity closure is not overridden."; }
		[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.HandleLobbyTimerExtensionRequest))]
		private static class OnOffer
		{
			private static void Postfix(LobbyBehaviour __instance, int __0, bool __1, int __2, int __3)
			{
				int instance = __instance.GetInstanceID();
				bool changed = lobbyInstance != instance || game != AmongUsClient.Instance?.GameId || host != __2 || offeredId != __3 || available != (__1 && __0 > 0);
				if(lobbyInstance != instance || game != AmongUsClient.Instance?.GameId || host != __2) Reset();
				lobbyInstance = instance; offeredId = __3; host = __2;
				game = AmongUsClient.Instance == null ? 0 : AmongUsClient.Instance.GameId;
				available = __1 && __0 > 0; expires = Time.realtimeSinceStartup + __0;
				if(changed) AUAnticheatPlugin.Log.LogWarning($"Lobby extension offer: available={available}, seconds={__0}, offer={__3}, hostMatches={AmongUsClient.Instance?.HostId == __2}.");
				if(!available) Status = "The server has not offered another extension. Start the game before the lobby expires.";
			}
		}
		[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.LobbyTimerExtended))]
		private static class OnExtended
		{
			private static void Postfix(LobbyBehaviour __instance)
			{
				var client = AmongUsClient.Instance;
				if(__instance.GetInstanceID() != lobbyInstance || client == null || client.GameId != game || client.HostId != host || requestedId < 0) return;
				available = false;
				awaitingConfirmation = false;
				Status = "The game confirmed a lobby extension. The server still controls the closing time.";
				AUAnticheatPlugin.Log.LogWarning("Lobby extension confirmation received.");
			}
		}
		public static void Tick()
		{
			var client = AmongUsClient.Instance; var lobby = LobbyBehaviour.Instance;
			if(client == null || lobby == null) { Reset(); return; }
			if(client.GameId != game || client.HostId != host || lobby.GetInstanceID() != lobbyInstance)
			{
				Reset(); game = client.GameId; host = client.HostId; lobbyInstance = lobby.GetInstanceID();
			}
			if(awaitingConfirmation && Time.realtimeSinceStartup - requestedAt >= 10)
			{
				awaitingConfirmation = false;
				Status = "No extension confirmation received. The lobby may still close; start before the countdown ends.";
				AUAnticheatPlugin.Log.LogWarning("Lobby extension request timed out without confirmation.");
			}
			if(!Enabled || !available || client == null || !client.AmHost || client.HostId != host || client.GameId != game || lobby == null ||
				lobby.GetInstanceID() != lobbyInstance || client.GameState != InnerNet.InnerNetClient.GameStates.Joined ||
				(GameManager.Instance != null && GameManager.Instance.GameHasStarted)) return;
			float remaining = expires - Time.realtimeSinceStartup;
			if(remaining <= 0) { available = false; Status = "Extension offer expired. No extension was confirmed."; return; }
			// Request as soon as offered: waiting until the last 30 seconds can
			// miss a short availability window. Still only one request per offer.
			if(requestedId == offeredId || Time.realtimeSinceStartup < nextAllowedRequest) return;
			requestedId = offeredId;
			// A fresh offer can expire within the old 30-second gate. Keep a paced
			// request interval and one request per offer, without missing short offers.
			nextAllowedRequest = Time.realtimeSinceStartup + 5;
			awaitingConfirmation = true; requestedAt = Time.realtimeSinceStartup;
			Status = "Extension requested; waiting for the game's confirmation. Do not assume the countdown stopped.";
			lobby.RpcExtendLobbyTimer(); // A synchronous confirmation must not be overwritten.
			AUAnticheatPlugin.notifications.Send("Lobby timer", "Requested the game's available lobby extension. Players stay in this lobby; the server decides whether to extend it.");
		}
	}
}
