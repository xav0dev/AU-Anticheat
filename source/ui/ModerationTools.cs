using UnityEngine;
using AUAnticheat.anticheat;
namespace AUAnticheat.ui
{
    internal static class ModerationTools
    {
        public static void Render()
        {
            RemovalRequestGuard.Enabled = Styles.Toggle(RemovalRequestGuard.Enabled,"Guard local kick/ban and late-rejection requests");
            Styles.Help($"Local removal requests stopped: {RemovalRequestGuard.Blocked}. This does not cancel server removals or protect other clients from every remote removal attack.");
            ChatModeration.Ads = Styles.Toggle(ChatModeration.Ads,"Hide lobby ads locally (never ban)");
            ChatModeration.Spam = Styles.Toggle(ChatModeration.Spam,"Hide chat floods locally");
            ChatModeration.WordFilter = Styles.Toggle(ChatModeration.WordFilter,"Use saved word filter");
            ChatModeration.NameFilter = Styles.Toggle(ChatModeration.NameFilter,"Hide chat from filtered names");
            ModerationStore.EnforceBans = Styles.Toggle(ModerationStore.EnforceBans,"Enforce my persistent ban list when hosting");
            ChatModeration.AfkAlerts = Styles.Toggle(ChatModeration.AfkAlerts,"Show possible AFK players in lobby (alert only)");
            ChatModeration.AfkSeconds = (int)GUILayout.HorizontalSlider(ChatModeration.AfkSeconds,60,600);
            Styles.Help($"AFK alert after {ChatModeration.AfkSeconds}s without observed movement/chat. Never kicks automatically; lag and menus can look idle.");
            Styles.Help($"Hidden locally: {ChatModeration.Hidden}. Last reason: {ChatModeration.LastReason}. Other players may still receive filtered chat on official servers. No cheat points or automatic bans for chat filters.");
            Styles.Help("Watchlist = private review reminder, not evidence. Block = hide their chat locally. Persistent ban = host-only admission policy; friend codes are game-reported, not authenticated identities.");
            if(GUILayout.Button("Reload moderation lists and filters")) ModerationStore.Reload();
            Styles.Help("Edit filtered-words.txt or filtered-names.txt (one literal phrase per line), then reload. Folder: " + ModerationStore.DirectoryPath);
            foreach(var entry in ModerationStore.Snapshot())
                if(GUILayout.Button($"Remove {entry.Kind}: {entry.FriendCode}")) ModerationStore.Set(entry.FriendCode,entry.Kind,false);
            Styles.Help(ModerationStore.Status);
        }
        public static void Selected(PlayerControl player)
        {
            string code = ChatModeration.BoundCode(player);
            if(code == null) { Styles.Help("Local lists require a matching, loaded friend code."); return; }
            foreach(string kind in new[]{"block","watch"})
            {
                bool contains = ModerationStore.Contains(code,kind);
                if(GUILayout.Button((contains ? "Remove local " : "Add local ") + kind)) ModerationStore.Set(code,kind,!contains);
            }
        }
    }
}
