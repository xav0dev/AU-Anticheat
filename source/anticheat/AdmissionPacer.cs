using UnityEngine;
namespace AUAnticheat.anticheat
{
    // Shared by entry-level, saved-ban, name/chat and online-list policies.
    // Security packet validation is independent; this only schedules moderation.
    internal static class AdmissionPacer
    {
        private static string room="";
        private static int owner=-1;
        private static uint net;
        private static float next, sentAt, last;
        private static bool paused;
        public static bool Ready()
        {
            var client=AmongUsClient.Instance;
            if(client==null || !client.AmHost || client.ClientId!=client.HostId) return false;
            float now=Time.realtimeSinceStartup;
            string key=$"{client.GameId}:{client.HostId}:{LobbyBehaviour.Instance?.GetInstanceID()}";
            if(key!=room || now<last) { room=key; owner=-1; paused=false; next=now; }
            last=now;
            if(paused) return false;
            if(owner>=0)
            {
                var p=client.FindClientById(owner)?.Character;
                if(p==null || p.NetId!=net || p.Data==null || p.Data.Disconnected) { owner=-1; next=now+2; }
                else if(now-sentAt>=15)
                {
                    paused=true;
                    AUAnticheatPlugin.notifications.Send("Moderation paused","A removal was not confirmed. Automatic admission/filter removals are paused until the lobby changes; no retry flood was sent.");
                }
                return false;
            }
            return now>=next;
        }
        public static void Sent(PlayerControl player) { owner=player.OwnerId; net=player.NetId; sentAt=Time.realtimeSinceStartup; next=sentAt+2; }
    }
}
