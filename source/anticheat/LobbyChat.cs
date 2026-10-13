using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace AUAnticheat.anticheat
{
	// Ordinary chat from the host's own player. Never impersonates a server,
	// bypasses dead/quick-chat restrictions, or exposes roles during a round.
	internal static class LobbyChat
	{
		public static bool ShareAlerts = true;
		private const string Prefix = "[AU-Anticheat] ";
		internal const int MaximumPublicMessageLength = 100;
		internal static string CombinedMessage(IReadOnlyList<string> lines)
		{
			// Clean each rule without truncation; retain separators in one chat bubble.
			var cleaned = new List<string>();
			foreach(string line in lines)
			{
				string rule = PublicText.Clean(line, int.MaxValue);
				if(rule.Length > 0) cleaned.Add(rule);
			}
			return string.Join(" / ", cleaned);
		}
		private sealed class Pending { public string Text; public float Expires; public bool LobbyOnly, Rules; }
		private static readonly Queue<Pending> PendingMessages = new();
		private static string session = "";
		private static float nextSend;
		private static string sendingText;
		private static PlayerControl sendingPlayer;
		private static bool renderedDuringSend;
		private sealed class Echo { public string Text, Room; public uint NetId; public float Until; public bool Shown; }
		private static readonly List<Echo> Echoes = new();
		// Some game builds render RpcSendChat locally. Only add a fallback bubble
		// when that did not happen; never suppress ordinary incoming/manual chat.
		[HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
		private static class ObserveChatRender
		{
			private static bool Prefix(PlayerControl __0, string __1)
			{
				if(sendingText != null && __0 == sendingPlayer && __1 == sendingText) renderedDuringSend = true;
				Echoes.RemoveAll(e => e.Until < Time.realtimeSinceStartup || e.Room != Key);
				if(__0 == null || __0 != PlayerControl.LocalPlayer) return true;
				foreach(var echo in Echoes)
				{
					if(echo.NetId != __0.NetId || echo.Text != __1) continue;
					if(echo.Shown) return false;
					echo.Shown = true; return true;
				}
				return true;
			}
		}
		private static string Key => AmongUsClient.Instance == null ? "offline" : $"{AmongUsClient.Instance.GameId}:{AmongUsClient.Instance.HostId}";
		// Clearing a queue or changing rooms must not shorten an outstanding send cooldown.
		public static void Clear() { PendingMessages.Clear(); session = Key; }
		private static bool Host => AUAnticheatPlugin.ProtectionsAttached && AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;
		private static float SendInterval => Math.Max(3f, ChatController.MAX_CHAT_SEND_RATE) + 0.25f;
		internal static bool LocalReady
		{
			get
			{
				var client = AmongUsClient.Instance;
				var local = PlayerControl.LocalPlayer;
				if(!Host || local == null || local.Data == null || local.Data.Disconnected || local.Data.IsDead ||
					client.ClientId != client.HostId || local.OwnerId != client.ClientId || HudManager.Instance?.Chat == null) return false;
				var peer = client.FindClientById(client.ClientId);
				return peer != null && !peer.IsBeingCreated && peer.Character == local;
			}
		}
		internal static bool WaitingLobby => LocalReady && LobbyBehaviour.Instance != null && MeetingHud.Instance == null &&
			AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Joined &&
			(GameManager.Instance == null || !GameManager.Instance.GameHasStarted) &&
			GameStartManager.Instance != null && GameStartManager.Instance.startState == GameStartManager.StartingStates.NotStarting;
		// Successful ordinary host chat also postpones automation. Never change a rejected send.
		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
		private static class ObserveLocalChat
		{
			private static void Prefix(PlayerControl __instance, string __0)
			{
				// A new manual send is intentional, even when it repeats automated text.
				if(sendingText == null && __instance == PlayerControl.LocalPlayer) Echoes.RemoveAll(e => e.Text == __0);
			}
			private static void Postfix(PlayerControl __instance, bool __result)
			{
				if(__result && __instance == PlayerControl.LocalPlayer) RecordSend(false);
			}
		}
		private static void RecordSend(bool consumeCooldown)
		{
			float interval = SendInterval;
			if(float.IsFinite(interval)) nextSend = Math.Max(nextSend, Time.realtimeSinceStartup + interval);
			// Consume the normal cooldown, including for the player's next manual message.
			// Never advance this timer to make a send eligible sooner.
			if(consumeCooldown && HudManager.Instance?.Chat != null) HudManager.Instance.Chat.timeSinceLastMessage = 0;
		}
		private static void Ensure() { if(session != Key) Clear(); }
		public static void QueueNotice(string text)
		{
			Ensure(); if(!ShareAlerts || !Host || PendingMessages.Count >= 16) return;
			PendingMessages.Enqueue(new Pending { Text = Prefix + PublicText.Clean(text, 100-Prefix.Length), Expires = Time.realtimeSinceStartup + 20 });
		}
		public static void ProtectionNotice()
		{
			// Unattributed blocks stay in the local Alerts panel, never public chat.
		}
		public static void EvidenceNotice(string name, string action, string reason)
		{
			if(action != "banned" && action != "kicked" && action != "detected") return;
			string lead = PublicText.Clean(name,16) + " got " + action + " by anticheat for ";
			QueueNotice(lead + PublicText.Clean(reason,100-Prefix.Length-lead.Length));
		}
		public static bool QueueReport(IReadOnlyList<string> lines)
		{
			Ensure(); if(!Host || lines.Count == 0 || lines.Count > 16 || PendingMessages.Count >= 16) return false;
			string message = CombinedMessage(lines);
			if(message.Length == 0 || message.Length > MaximumPublicMessageLength) return false;
			foreach(var queued in PendingMessages) if(queued.LobbyOnly && !queued.Rules && queued.Text == message) return true;
			PendingMessages.Enqueue(new Pending { Text = message, Expires = Time.realtimeSinceStartup + 90, LobbyOnly = true });
			return true;
		}
		public static void CancelRules()
		{
			int count = PendingMessages.Count;
			while(count-- > 0) { var message = PendingMessages.Dequeue(); if(!message.Rules) PendingMessages.Enqueue(message); }
		}
		public static bool QueueRules(IReadOnlyList<string> lines)
		{
			Ensure(); if(!Host || !LobbyRules.CanSend || lines.Count == 0 || lines.Count > 8 || PendingMessages.Count >= 16) return false;
			string message = CombinedMessage(lines);
			if(message.Length == 0 || message.Length > MaximumPublicMessageLength) return false;
			foreach(var queued in PendingMessages) if(queued.Rules && queued.Text == message) return true;
			PendingMessages.Enqueue(new Pending { Text = message, Expires = Time.realtimeSinceStartup + 90, LobbyOnly = true, Rules = true });
			return true;
		}
		public static void Tick()
		{
			Ensure(); if(!Host) { PendingMessages.Clear(); return; }
			float now = Time.realtimeSinceStartup;
			while(PendingMessages.Count > 0 && (PendingMessages.Peek().Expires < now ||
				(PendingMessages.Peek().Rules ? !LobbyRules.CanSend : PendingMessages.Peek().LobbyOnly ? !RoundReports.Enabled : !ShareAlerts))) PendingMessages.Dequeue();
			if(PendingMessages.Count == 0 || now < nextSend) return;
			bool waiting = WaitingLobby;
			var local = PlayerControl.LocalPlayer;
			if(PendingMessages.Peek().LobbyOnly && (AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started ||
				(GameManager.Instance != null && GameManager.Instance.GameHasStarted))) { PendingMessages.Dequeue(); return; }
			if(!LocalReady || (!waiting && (MeetingHud.Instance == null ||
				AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Started ||
				GameManager.Instance == null || !GameManager.Instance.GameHasStarted))) return;
			if(PendingMessages.Peek().LobbyOnly && !waiting) return;
			if(PendingMessages.Peek().LobbyOnly && !PendingMessages.Peek().Rules && !RoundReports.ReadyToSend) return;
			var chat = HudManager.Instance.Chat;
			float interval = SendInterval;
			if(!float.IsFinite(interval) || !float.IsFinite(chat.timeSinceLastMessage) || chat.timeSinceLastMessage < interval ||
				AmongUs.Data.DataManager.Settings.Multiplayer.ChatMode == InnerNet.QuickChatModes.QuickChatOnly) return;
			nextSend = now + interval;
			// A rejected send is not retried indefinitely. Ordinary chat restrictions win.
			var pending = PendingMessages.Dequeue();
			sendingText = pending.Text; sendingPlayer = local; renderedDuringSend = false;
			Echoes.RemoveAll(e => e.Until < now || e.Room != Key || (e.NetId == local.NetId && e.Text == pending.Text));
			if(Echoes.Count >= 16) Echoes.RemoveAt(0);
			var receipt = new Echo { Text = pending.Text, Room = Key, NetId = local.NetId, Until = now + 15 };
			Echoes.Add(receipt);
			try
			{
				if(local.RpcSendChat(pending.Text))
				{
					RecordSend(true);
					if(pending.Rules) LobbyRules.MarkSent();
					if(!renderedDuringSend) chat.AddChat(local, pending.Text);
				}
				else Echoes.Remove(receipt);
			}
			finally { sendingText = null; sendingPlayer = null; renderedDuringSend = false; }
		}
	}
}
