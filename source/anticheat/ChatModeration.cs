using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace AUAnticheat.anticheat
{
    internal static class ChatModeration
    {
        public static bool Ads = true, Spam = true, WordFilter, NameFilter, AfkAlerts;
        public static int AfkSeconds = 120;
        public static long Hidden { get; private set; }
        public static string LastReason { get; private set; } = "None";
        private sealed class State { public uint NetId; public readonly Queue<float> Times = new(); }
        private static readonly Dictionary<int,State> States = new();
        private static string room = "";
        private static float noticeAt;
        public static string BoundCode(PlayerControl player)
        {
            if(player == null) return null;
            var peer = AmongUsClient.Instance?.FindClientById(player.OwnerId);
            if(peer == null || peer.IsBeingCreated || peer.Character != player || player.Data == null) return null;
            string code = FriendCodeExceptions.Normalize(peer.FriendCode);
            return code != null && code == FriendCodeExceptions.Normalize(player.Data.FriendCode) ? code : null;
        }
        [HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
        private static class FilterDisplayedChat
        {
            private static bool Prefix(PlayerControl __0, string __1) => Allow(__0,__1);
        }
        internal static bool Allow(PlayerControl player, string message)
        {
            if(player == null || player == PlayerControl.LocalPlayer || player.Data == null || message == null) return true;
            LobbyIdleMonitor.Touch(player);
            string current = AmongUsClient.Instance == null ? "" : $"{AmongUsClient.Instance.GameId}:{AmongUsClient.Instance.HostId}";
            if(current != room) { room = current; States.Clear(); }
            string reason = null;
            if(ModerationStore.Contains(BoundCode(player), "block")) reason = "your local block list";
            else if(Ads && ChatContentRules.IsAd(message)) reason = "lobby recruitment or invite link";
            else if(WordFilter && ChatContentRules.MatchesLiteral(message, ModerationStore.Words)) reason = "your word filter";
            else if(NameFilter && ChatContentRules.MatchesLiteral(player.Data.PlayerName, ModerationStore.Names)) reason = "your name filter";
            float now = Time.realtimeSinceStartup;
            if(Spam)
            {
                if(!States.TryGetValue(player.OwnerId, out var state) || state.NetId != player.NetId)
                {
                    if(States.Count >= 64) States.Clear();
                    States[player.OwnerId] = state = new State { NetId = player.NetId };
                }
                while(state.Times.Count > 0 && now - state.Times.Peek() >= 10) state.Times.Dequeue();
                if(state.Times.Count >= 5) reason ??= "chat flooding";
                if(state.Times.Count < 6) state.Times.Enqueue(now);
            }
            if(reason == null) return true;
            // Private blocks never cause removals. Optional host responses are
            // moderation preferences, not evidence of cheating or authenticated senders.
            if(reason == "your word filter" || reason == "chat flooding") HostModeration.RequestAction(player,HostModeration.ChatResponse,reason);
            else if(reason == "your name filter") HostModeration.RequestAction(player,HostModeration.NameResponse,"name filter");
            else if(reason == "lobby recruitment or invite link") HostModeration.RequestAction(player,HostModeration.AdResponse,reason);
            Hidden++; LastReason = reason;
            if(now >= noticeAt)
            {
                noticeAt = now + 10;
                AUAnticheatPlugin.notifications.Send("Chat filter", $"Hidden chat displayed as {PublicText.Name(player.Data.PlayerName)}: {reason}. Sender identity is not authenticated; other clients may receive it. Host removal follows your selected filter response, never cheat points.");
            }
            return false;
        }
    }
}
