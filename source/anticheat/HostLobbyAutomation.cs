using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace AUAnticheat.anticheat
{
    internal static class HostLobbyAutomation
    {
        public static bool JoinFloodProtection = true, AutoLock = true, AutoStart, AutoReturn;
        public static int JoinLimit = 8, JoinWindowSeconds = 10, StartPlayers, StartDelaySeconds = 10;
        public static string Status = "Auto-start off. Full lobby is the default target.";
        private static readonly EventWindow joins = new();
        private static string room = "", roster = "";
        private static float since = -1, joinGrace, lockUntil, lockAttemptAt;
        private static bool startRequested;
        private static EndGameManager endScreen;
        private static int endRoom, endHost;
        private static float returnAt;
        private static void Ensure()
        {
            string current=$"{AmongUsClient.Instance?.GameId}:{AmongUsClient.Instance?.HostId}:{LobbyBehaviour.Instance?.GetInstanceID()}";
            if(room == current) return;
            room=current; joins.Clear(); roster=""; since=-1; joinGrace=Time.realtimeSinceStartup+10; lockUntil=0; startRequested=false;
        }
        public static void Joined(InnerNet.ClientData peer)
        {
            Ensure();
            var client=AmongUsClient.Instance;
            if(!JoinFloodProtection || peer == null || client == null || !client.AmHost || peer.Id == client.HostId || LobbyBehaviour.Instance == null) return;
            float now=Time.realtimeSinceStartup;
            // Do not mistake the initial roster sync/Play Again for a join flood.
            if(now < joinGrace) return;
            if(joins.Add(now,Math.Clamp(JoinWindowSeconds,5,60)) < Math.Clamp(JoinLimit,4,30)) return;
            lockUntil=now+30; since=-1;
            Status="Join flood observed. Auto-start paused for 30 seconds. No player accused or banned.";
            if(now >= lockAttemptAt)
            {
                lockAttemptAt=now+10;
                if(AutoLock && client.ClientId == client.HostId && client.IsGamePublic) { client.ChangeGamePublic(false); Status += " Requested private lobby; code/invite joins may still work."; }
                AUAnticheatPlugin.notifications.Send("Join protection",Status);
            }
        }
        public static void LockNow()
        {
            var client=AmongUsClient.Instance;
            if(client == null || !client.AmHost || client.ClientId != client.HostId || LobbyBehaviour.Instance == null) return;
            client.ChangeGamePublic(false); Status="Requested private lobby. Existing players stay; code/invite joins are not blocked.";
        }
        [HarmonyPatch(typeof(EndGameManager), nameof(EndGameManager.Start))]
        private static class ResultScreen
        {
            private static void Postfix(EndGameManager __instance)
            {
                endScreen=__instance; returnAt=Time.realtimeSinceStartup+8;
                endRoom=AmongUsClient.Instance?.GameId ?? 0; endHost=AmongUsClient.Instance?.HostId ?? -1;
            }
        }
        public static void Tick()
        {
            Ensure();
            var client=AmongUsClient.Instance;
            float now=Time.realtimeSinceStartup;
            if(endScreen != null && now >= returnAt)
            {
                var screen=endScreen; endScreen=null; // One ordinary UI action, never a retry loop.
                if(AutoReturn && client != null && client.GameState == InnerNet.InnerNetClient.GameStates.Ended && client.GameId == endRoom && client.HostId == endHost && LobbyBehaviour.Instance == null && screen.Navigation != null)
                    screen.Navigation.NextGame();
            }
            if(!AutoStart || !LobbyChat.WaitingLobby || now < lockUntil) { since=-1; roster=""; startRequested=false; return; }
            int capacity=GameOptionsManager.Instance.CurrentGameOptions.MaxPlayers;
            int target=Beta2Rules.StartTarget(StartPlayers,capacity,GameStartManager.Instance.MinPlayers);
            var ids=new List<string>();
            foreach(var player in PlayerControl.AllPlayerControls)
            {
                if(player?.Data == null || player.Data.Disconnected) continue;
                var peer=client.FindClientById(player.OwnerId);
                if(peer == null || peer.IsBeingCreated || peer.Character != player || !peer.IsReady) { since=-1; return; }
                ids.Add($"{player.OwnerId}:{player.NetId}");
            }
            if(ids.Count < target) { since=-1; roster=""; startRequested=false; return; }
            ids.Sort(StringComparer.Ordinal); string current=string.Join(",",ids);
            if(since < 0 || roster != current) { roster=current; since=now; startRequested=false; }
            int delay=Math.Clamp(StartDelaySeconds,5,120);
            Status=$"Auto-start: {ids.Count}/{target}, stable for {(int)(now-since)}/{delay}s.";
            if(!startRequested && now-since >= delay)
            {
                startRequested=true;
                GameStartManager.Instance.BeginGame(); // Keep the game's normal checks and countdown.
                Status="Requested the normal game-start countdown.";
            }
        }
    }
}
