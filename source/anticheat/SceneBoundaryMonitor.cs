using System;
using System.Collections.Generic;
using UnityEngine;
namespace AUAnticheat.anticheat
{
    internal static class SceneBoundaryMonitor
    {
        private sealed class Observation { public uint NetId; public float Ready,OutsideSince,LastNotice=-100; public int Count; }
        private static readonly Dictionary<int,Observation> players=new();
        private static string session="";
        private static float nextGeometry;
        private static Bounds bounds;
        private static bool valid;
        private static bool Refresh()
        {
            var client=AmongUsClient.Instance;
            Component root=LobbyBehaviour.Instance != null ? LobbyBehaviour.Instance : ShipStatus.Instance;
            if(client==null || root==null) { session=""; players.Clear(); valid=false; return false; }
            string key=$"{client.GameId}:{client.HostId}:{root.GetInstanceID()}";
            if(key!=session) { session=key; players.Clear(); valid=false; nextGeometry=0; }
            if(Time.realtimeSinceStartup<nextGeometry) return valid;
            nextGeometry=Time.realtimeSinceStartup+1; valid=false;
            var colliders=root.GetComponentsInChildren<Collider2D>();
            if(colliders==null || colliders.Length>2048 || Constants.ShipOnlyMask==0) return false;
            int count=0;
            foreach(var collider in colliders)
            {
                if(collider==null || !collider.enabled || collider.isTrigger ||
                   (Constants.ShipOnlyMask & (1<<collider.gameObject.layer))==0 ||
                   (collider.attachedRigidbody!=null && collider.attachedRigidbody.bodyType!=RigidbodyType2D.Static)) continue;
                if(count++==0) bounds=collider.bounds; else bounds.Encapsulate(collider.bounds);
            }
            valid=SceneBoundaryRules.Usable(bounds.min.x,bounds.min.y,bounds.max.x,bounds.max.y,count);
            return valid;
        }
        public static bool OutsideLobby(Vector2 point)
        {
            try { return LobbyBehaviour.Instance != null && Refresh() && Outside(point); }
            catch(Exception) { valid=false; return false; } // Missing geometry must not convict.
        }
        private static bool Outside(Vector2 point) => SceneBoundaryRules.Outside(point.x,point.y,bounds.min.x,bounds.min.y,bounds.max.x,bounds.max.y);
        public static void Tick()
        {
            bool lobby=LobbyBehaviour.Instance!=null;
            if(!(lobby?LobbyMovementGuard.Enabled:Anticheat.Enabled) || !Refresh()) return;
            float now=Time.realtimeSinceStartup;
            var present=new HashSet<int>();
            foreach(var player in PlayerControl.AllPlayerControls)
            {
                if(player?.Data==null || player.Data.Disconnected) continue;
                present.Add(player.OwnerId);
                if(!players.TryGetValue(player.OwnerId,out var sample) || sample.NetId!=player.NetId)
                {
                    if(players.Count>=64) continue;
                    players[player.OwnerId]=sample=new Observation { NetId=player.NetId,Ready=now+5 };
                }
                bool stable=lobby ? !player.Data.IsDead && player.MyPhysics!=null && !player.MyPhysics.DoingCustomAnimation : MovementContext.StableLivingPlayer(player);
                if(!stable || now<sample.Ready || !Outside(player.GetTruePosition())) { sample.Count=0; continue; }
                if(sample.Count++==0) sample.OutsideSince=now;
                if(!SceneBoundaryRules.Sustained(sample.Count,now-sample.OutsideSince) || now-sample.LastNotice<15) continue;
                sample.LastNotice=now;
                using var evidence=SenderEvidence.Begin("Scene boundary observation",null,player.NetId,player.OwnerId,PublicText.Name(player.Data.PlayerName));
                SenderEvidence.Record();
                var position=player.GetTruePosition();
                AUAnticheatPlugin.notifications.Send("Protection",$"Position ({position.x:F1}, {position.y:F1}) stayed beyond the loaded scene's outer bounds. Possible invalid movement or desync; sender unknown, no ban.");
            }
            foreach(int id in new List<int>(players.Keys)) if(!present.Contains(id)) players.Remove(id);
        }
    }
}
