using System;
using System.Collections.Generic;
using UnityEngine;

namespace AUAnticheat.anticheat
{
    internal static class LobbyIdleMonitor
    {
        private sealed class Idle { public uint NetId; public float X,Y,Since; public bool Warned; }
        private static readonly Dictionary<int,Idle> Players = new();
        private static string room = "";
        public static void Touch(PlayerControl player)
        {
            if(player != null && Players.TryGetValue(player.OwnerId,out var state) && state.NetId == player.NetId)
            { state.Since = Time.realtimeSinceStartup; state.Warned = false; }
        }
        public static void Tick()
        {
            if(!ChatModeration.AfkAlerts || !LobbyChat.WaitingLobby) { Players.Clear(); room = ""; return; }
            string current = $"{AmongUsClient.Instance.GameId}:{LobbyBehaviour.Instance.GetInstanceID()}";
            if(current != room) { Players.Clear(); room = current; }
            var present = new HashSet<int>();
            foreach(var player in PlayerControl.AllPlayerControls)
            {
                if(player == null || player.Data == null || player.Data.Disconnected || player == PlayerControl.LocalPlayer || player.OwnerId == AmongUsClient.Instance.HostId) continue;
                present.Add(player.OwnerId);
                var position = player.GetTruePosition();
                if(!float.IsFinite(position.x) || !float.IsFinite(position.y)) continue;
                if(!Players.TryGetValue(player.OwnerId,out var state) || state.NetId != player.NetId)
                    Players[player.OwnerId] = state = new Idle { NetId = player.NetId, X = position.x, Y = position.y, Since = Time.realtimeSinceStartup };
                float dx = position.x-state.X, dy = position.y-state.Y;
                if(dx*dx+dy*dy > 0.01f) { state.X=position.x; state.Y=position.y; Touch(player); }
                if(!state.Warned && Time.realtimeSinceStartup-state.Since >= ChatModeration.AfkSeconds)
                {
                    state.Warned=true;
                    AUAnticheatPlugin.notifications.Send("Possible AFK", $"{PublicText.Name(player.Data.PlayerName)} has no observed movement/chat for {ChatModeration.AfkSeconds}s. Review manually; menus or lag can look idle. No kick sent.");
                }
            }
            foreach(int id in new List<int>(Players.Keys)) if(!present.Contains(id)) Players.Remove(id);
        }
    }
}
