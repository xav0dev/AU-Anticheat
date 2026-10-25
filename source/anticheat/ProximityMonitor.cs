using System;
using System.Collections.Generic;
using UnityEngine;

namespace AUAnticheat.anticheat
{
    // Proximity is ordinary gameplay, never proof of teaming or cheat evidence.
    internal static class ProximityMonitor
    {
        public static bool Enabled;
        private sealed class Sample { public float Last,Since,DistanceA,DistanceB,Notice=-1000; public Vector2 A,B; public PlayerControl PlayerA,PlayerB; public bool Tracking; }
        private static readonly Dictionary<string,Sample> pairs=new();
        private static string room="";
        private static float next;
        public static bool Qualifies(PlayerControl player)
        {
            if(!Enabled || !Anticheat.Enabled || player?.Data==null || LobbyBehaviour.Instance!=null || ShipStatus.Instance==null ||
                room!=$"{AmongUsClient.Instance?.GameId}:{AmongUsClient.Instance?.HostId}:{ShipStatus.Instance.GetInstanceID()}" ||
                !MovementContext.StableLivingPlayer(player)) return false;
            foreach(var sample in pairs.Values)
            {
                var other=sample.PlayerA==player?sample.PlayerB:sample.PlayerB==player?sample.PlayerA:null;
                if(other?.Data==null || other.Data.Disconnected || player.Data.Disconnected || !MovementContext.StableLivingPlayer(other)) continue;
                float distance=Distance(player.GetTruePosition(),other.GetTruePosition());
                float age=Time.realtimeSinceStartup-sample.Last;
                if(float.IsFinite(distance) && distance<=1.5f && age>=0 && age<=1 && sample.Tracking &&
                    Time.realtimeSinceStartup-sample.Since>=30 && sample.DistanceA>=5 && sample.DistanceB>=5) return true;
            }
            return false;
        }
        private static float Distance(Vector2 a,Vector2 b) => MathF.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y));
        public static void Tick()
        {
            if(!Enabled || !Anticheat.Enabled || LobbyBehaviour.Instance!=null || ShipStatus.Instance==null)
            { pairs.Clear(); next=0; return; }
            float now=Time.realtimeSinceStartup;
            string current=$"{AmongUsClient.Instance?.GameId}:{AmongUsClient.Instance?.HostId}:{ShipStatus.Instance.GetInstanceID()}";
            if(room!=current) { room=current; pairs.Clear(); next=0; }
            if(now<next) return; next=now+.5f;
            var players=new List<PlayerControl>();
            foreach(var player in PlayerControl.AllPlayerControls)
                if(player?.Data!=null && !player.Data.Disconnected && MovementContext.StableLivingPlayer(player) && players.Count<15) players.Add(player);
            players.Sort((a,b)=>a.NetId.CompareTo(b.NetId));
            var present=new HashSet<string>();
            for(int i=0;i<players.Count;i++) for(int j=i+1;j<players.Count;j++)
            {
                var a=players[i]; var b=players[j]; string key=$"{a.OwnerId}:{a.NetId}:{b.OwnerId}:{b.NetId}"; present.Add(key);
                if(!pairs.TryGetValue(key,out var s)) pairs[key]=s=new Sample {PlayerA=a,PlayerB=b};
                Vector2 pa=a.GetTruePosition(),pb=b.GetTruePosition();
                float distance=Distance(pa,pb),da=Distance(pa,s.A),db=Distance(pb,s.B);
                if(!float.IsFinite(distance) || distance>1.5f || now-s.Last>2 || now<s.Last || da>3 || db>3)
                    s.Tracking=false;
                else
                {
                    if(!s.Tracking) { s.Since=now; s.DistanceA=s.DistanceB=0; s.Tracking=true; }
                    s.DistanceA+=da; s.DistanceB+=db;
                    if(now-s.Since>=30 && s.DistanceA>=5 && s.DistanceB>=5 && now-s.Notice>=120)
                    {
                        s.Notice=now;
                        AUAnticheatPlugin.notifications.Send("Proximity observation",PublicText.Name(a.Data.PlayerName)+" and "+PublicText.Name(b.Data.PlayerName)+" stayed nearby while moving for 30 seconds. This can be normal cooperation. Selected response: "+HostModeration.ProximityResponse+"; no cheat points or online report.");
                        HostModeration.RequestAction(a,HostModeration.ProximityResponse,"sustained player proximity");
                        HostModeration.RequestAction(b,HostModeration.ProximityResponse,"sustained player proximity");
                    }
                }
                s.A=pa; s.B=pb; s.Last=now;
            }
            foreach(string key in new List<string>(pairs.Keys)) if(!present.Contains(key)) pairs.Remove(key);
        }
    }
}
