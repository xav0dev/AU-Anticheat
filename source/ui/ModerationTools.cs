using UnityEngine;
using AUAnticheat.anticheat;
namespace AUAnticheat.ui
{
    internal static class ModerationTools
    {
        private static string reportCode;
        public static void Render()
        {
            RemovalRequestGuard.Enabled = Styles.Toggle(RemovalRequestGuard.Enabled,"Guard local kick/ban and late-rejection requests");
            Styles.Help($"Local removal requests stopped: {RemovalRequestGuard.Blocked}. This does not cancel server removals or protect other clients from every remote removal attack.");
            ChatModeration.Ads = Styles.Toggle(ChatModeration.Ads,"Ad blocker: links and lobby promotions");
            Response("Ad blocker response",ref HostModeration.AdResponse);
            Styles.Help("Hides matching chat here. Alert is the default; host removal is optional. On official servers other clients can receive the message before the host, so lobby-wide censorship cannot be guaranteed.");
            HostModeration.BotNameFilter=Styles.Toggle(HostModeration.BotNameFilter,"Match configured bot names (exact names)");
            HostModeration.DefaultNameFilter=Styles.Toggle(HostModeration.DefaultNameFilter,"Match configured default names (exact names)");
            Styles.Help("Bundled for every installation: HostGuard's TNT, auser and Haunt Bot names, plus its default-name word-pair detector. Add your own exact names in bot-names.txt/default-names.txt, then reload. Filters are optional: matching a name does not prove cheating.");
            Response("Chat-filter response",ref HostModeration.ChatResponse);
            Response("Name-filter response",ref HostModeration.NameResponse);
            Response("Bot-name response",ref HostModeration.BotNameResponse);
            Response("Default-name response",ref HostModeration.DefaultNameResponse);
            Styles.Help("Bot and default-name matches default to Ban. Your saved choices take precedence. These are host admission policies, not cheating evidence. Persistent Ban also saves the friend code locally.");
            Styles.Help(HostModeration.Status);
            CommunityServices.Enabled=Styles.Toggle(CommunityServices.Enabled,"Download optional Google Sheets community list");
            Styles.Help("Off by default. Google receives your connection IP. Downloads only friend code, status, reason and expiry; no player data is uploaded. Online status is not proof of cheating.");
            Response("Shared-list response",ref CommunityServices.Response);
            if(GUILayout.Button("Refresh community list (15-minute limit)")) CommunityServices.Refresh();
            Styles.Help(CommunityServices.Status);
            if(GUILayout.Button("Use AU-Anticheat review service")) CommunityServices.ReportingEndpoint=CommunityServices.DefaultReportingEndpoint;
            InlineEditor.Field("Reporting service URL",CommunityServices.ReportingEndpoint,512,value=>
            { if(value.Length!=0 && !CommunityListRules.ReportingUrl(value.Trim())) return false; CommunityServices.ReportingEndpoint=value.Trim(); return true; });
            Styles.Help("The AU-Anticheat service sends pending review requests to the maintainer's private inbox. You can use your own deployed /exec URL or leave blank to disable. Reports require preview and confirmation; nothing is uploaded automatically.");
            ModerationStore.EnforceBans = Styles.Toggle(ModerationStore.EnforceBans,"Enforce my persistent ban list when hosting");
            ChatModeration.AfkAlerts = Styles.Toggle(ChatModeration.AfkAlerts,"Watch for AFK players in lobby");
            ChatModeration.AfkSeconds = (int)GUILayout.HorizontalSlider(ChatModeration.AfkSeconds,60,600);
            Response("AFK response",ref HostModeration.AfkResponse);
            Styles.Help($"After {ChatModeration.AfkSeconds}s without observed movement/chat. Alert is the default. Lag and menus can look idle; activity is rechecked before a queued removal.");
            ProximityMonitor.Enabled=Styles.Toggle(ProximityMonitor.Enabled,"Observe sustained player proximity");
            Response("Player proximity response",ref HostModeration.ProximityResponse);
            Styles.Help("Alert is the default. Two living players moving together for 30 seconds may be cooperating normally. Optional host removals apply to BOTH matched players, excluding the host; never cheat points or online reports.");
            Styles.Help($"Hidden locally: {ChatModeration.Hidden}. Last reason: {ChatModeration.LastReason}. Other players may still receive filtered chat on official servers. Filter actions never add cheat points.");
            Styles.Help("Watchlist = private review reminder, not evidence. Block = hide their chat locally. Persistent ban = host-only admission policy; friend codes are game-reported, not authenticated identities.");
            if(GUILayout.Button("Reload moderation lists and filters")) ModerationStore.Reload();
            Styles.Help("Edit filtered-words.txt or filtered-names.txt (one literal phrase per line), then reload. Folder: " + ModerationStore.DirectoryPath);
            foreach(var entry in ModerationStore.Snapshot())
                if(GUILayout.Button($"Remove {entry.Kind}: {entry.FriendCode}")) ModerationStore.Set(entry.FriendCode,entry.Kind,false);
            Styles.Help(ModerationStore.Status);
        }
        public static void RenderPersonal()
        {
            ChatModeration.Ads=Styles.Toggle(ChatModeration.Ads,"Hide links and lobby promotions in my chat");
            ChatModeration.Spam=Styles.Toggle(ChatModeration.Spam,"Hide chat floods in my chat");
            ChatModeration.WordFilter=Styles.Toggle(ChatModeration.WordFilter,"Use my saved word filter");
            ChatModeration.NameFilter=Styles.Toggle(ChatModeration.NameFilter,"Hide chat from my filtered names");
            Styles.Help("Local filters also work as a guest. Host responses are in Lobby > Local moderation. These switches cannot erase messages on other players' devices.");
            Styles.Help($"Hidden messages: {ChatModeration.Hidden}. Latest reason: {ChatModeration.LastReason}.");
            if(GUILayout.Button("Reload my filters")) ModerationStore.Reload();
            Styles.Help("One phrase per line in filtered-words.txt and filtered-names.txt. Folder: "+ModerationStore.DirectoryPath);
            foreach(var entry in ModerationStore.Snapshot())
                if(entry.Kind=="block" && GUILayout.Button("Unblock chat: "+entry.FriendCode)) ModerationStore.Set(entry.FriendCode,"block",false);
        }
        private static void Response(string label,ref FilterResponse selected)
        {
            Styles.HeadingText(label);
            GUILayout.BeginHorizontal();
            try { foreach(var value in new[]{FilterResponse.HideOrAlert,FilterResponse.Kick,FilterResponse.Ban,FilterResponse.PersistentBan})
                if(GUILayout.Button((selected==value?"> ":"")+(value==FilterResponse.HideOrAlert?"Alert":value==FilterResponse.PersistentBan?"Persistent Ban":value.ToString()))) selected=value;
            } finally { GUILayout.EndHorizontal(); }
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
            if(GUILayout.Button("Review online report for this player")) reportCode=code;
            if(reportCode==code)
            {
                Styles.Help("Report preview: friend code "+code+"; suspected cheating, sender attribution unverified; plugin version. Sends to your configured Google Apps Script review endpoint, not the public ban list. Service receives your IP. No device IDs, chat logs or other players' data are included. Submit only after reviewing evidence.");
                Styles.Help("Endpoint: "+(string.IsNullOrEmpty(CommunityServices.ReportingEndpoint)?"Not configured":CommunityServices.ReportingEndpoint));
                if(GUILayout.Button("Submit this report for review")) { CommunityServices.SubmitReviewedReport(code); reportCode=null; }
                if(GUILayout.Button("Cancel report")) reportCode=null;
            }
            Styles.Help(CommunityServices.ReportStatus);
        }
    }
}
