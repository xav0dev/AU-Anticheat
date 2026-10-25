using HarmonyLib;
using InnerNet;

namespace AUAnticheat.anticheat
{
    // Guards requests made by THIS client, not server decisions or other clients'
    // outgoing traffic. Does not suppress OnPlayerLeft or fake a connected state.
    internal static class RemovalRequestGuard
    {
        public static bool Enabled = true;
        public static long Blocked { get; private set; }
        private static bool Deny(string reason, int id)
        {
            Blocked++;
            if(Blocked <= 20) AUAnticheatPlugin.Log.LogWarning($"Local removal request stopped: {reason}; affected lobby ID {id}. Caller is not authenticated attacker evidence.");
            return false;
        }
        [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.KickPlayer))]
        private static class BeforeKickOrBan
        {
            private static bool Prefix(InnerNetClient __instance, int __0)
            {
                if(!Enabled || __instance == null) return true;
                if(!__instance.AmHost || __instance.ClientId != __instance.HostId) return Deny("client has no settled host authority",__0);
                if(__0 == __instance.ClientId || __0 == __instance.HostId) return Deny("self/host targeted",__0);
                return true; // Ordinary authorized moderation remains available.
            }
        }
        [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.SendLateRejection))]
        private static class BeforeLateRejection
        {
            private static bool Prefix(InnerNetClient __instance, int __0)
            {
                if(!Enabled || __instance == null) return true;
                if(!__instance.AmHost || __instance.ClientId != __instance.HostId || __0 == __instance.ClientId || __0 == __instance.HostId) return Deny("unauthorized late rejection",__0);
                var target = __instance.FindClientById(__0);
                if(target != null && target.IsReady && !target.IsBeingCreated)
                    return Deny("late startup rejection aimed at an already-ready client",__0);
                return true; // Preserve legitimate startup timeout/failed-ready handling.
            }
        }
    }
}
