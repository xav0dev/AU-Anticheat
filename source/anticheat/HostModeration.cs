using System;
using System.Collections.Generic;
using UnityEngine;

namespace AUAnticheat.anticheat
{
    internal enum FilterResponse { HideOrAlert, Kick, Ban, PersistentBan }
    internal static class HostModeration
    {
        public static FilterResponse ChatResponse = FilterResponse.HideOrAlert, NameResponse = FilterResponse.HideOrAlert;
        public static bool BotNameFilter = true, DefaultNameFilter = true;
        public static FilterResponse BotNameResponse=FilterResponse.Ban, DefaultNameResponse=FilterResponse.Ban;
        public static FilterResponse AdResponse=FilterResponse.HideOrAlert, AfkResponse=FilterResponse.HideOrAlert, ProximityResponse=FilterResponse.HideOrAlert;
        private sealed class Request { public int Owner; public uint Net; public string Code, Reason; public FilterResponse Response; public float Until; }
        private static readonly Queue<Request> pending = new();
        private static string session = "";
        private static float next, sentAt;
        private static Request sent;
        private static bool paused;
        private static readonly HashSet<string> nameNotices = new();
        public static string Status = "Bot/default names default to Ban; AFK, ads and proximity default to Alert. Name and chat ownership can be spoofed.";
        private static string Key => $"{AmongUsClient.Instance?.GameId}:{AmongUsClient.Instance?.HostId}:{LobbyBehaviour.Instance?.GetInstanceID()}";
        private static bool Host => AUAnticheatPlugin.ProtectionsAttached && AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost && AmongUsClient.Instance.ClientId == AmongUsClient.Instance.HostId;
        private static void Ensure()
        {
            if(session == Key) return;
            session = Key; pending.Clear(); nameNotices.Clear(); sent = null; paused = false; next = Time.realtimeSinceStartup + 3;
        }
        public static void RememberCheater(string code,string name)
        {
            if(!Host || Anticheat.Punishment != Anticheat.Punishments.PersistentBan) return;
            if(code == null || !ModerationStore.Set(code, "ban", true))
                AUAnticheatPlugin.notifications.Send("Persistent ban", "Could not save the ban for " + PublicText.Name(name) + ". A loaded matching friend code and writable local list are required.");
        }
        public static void RequestAction(PlayerControl player, FilterResponse response, string reason)
        {
            Ensure();
            if(!Host || paused || response == FilterResponse.HideOrAlert || player?.Data == null || player == PlayerControl.LocalPlayer || player.OwnerId == AmongUsClient.Instance.HostId) return;
            string code = ChatModeration.BoundCode(player);
            if(code == null || pending.Count >= 32 || (sent?.Owner == player.OwnerId && sent.Net == player.NetId)) return;
            foreach(var item in pending) if(item.Owner == player.OwnerId && item.Net == player.NetId) return;
            pending.Enqueue(new Request { Owner=player.OwnerId, Net=player.NetId, Code=code, Response=response, Reason=reason, Until=Time.realtimeSinceStartup+30 });
        }
        public static void Tick()
        {
            Ensure(); if(!Host) { pending.Clear(); sent = null; return; }
            float now = Time.realtimeSinceStartup;
            bool admissionReady=AdmissionPacer.Ready();
            if(sent != null)
            {
                var p = AmongUsClient.Instance.FindClientById(sent.Owner)?.Character;
                if(p == null || p.NetId != sent.Net || p.Data == null || p.Data.Disconnected) { sent=null; next=now+3; }
                else if(now-sentAt >= 15) { paused=true; pending.Clear(); Status="Automatic filter removals paused: a removal was not confirmed. Rejoin a lobby to reset."; }
                return;
            }
            if(paused || now < next || !AmongUsClient.Instance.CanBan()) return;
            // Admission filters do not act during a round or a start countdown.
            if(LobbyChat.WaitingLobby)
                foreach(var player in PlayerControl.AllPlayerControls)
                {
                    if(player?.Data == null) continue;
                    string reason = NameReason(player.Data.PlayerName);
                    if(reason != null && player != PlayerControl.LocalPlayer && player.OwnerId != AmongUsClient.Instance.HostId)
                    {
                        string notice=$"{player.OwnerId}:{player.NetId}:{reason}";
                        if(nameNotices.Count<128 && nameNotices.Add(notice))
                            AUAnticheatPlugin.notifications.Send("Name filter",PublicText.Name(player.Data.PlayerName)+": "+reason+". This is a moderation preference, not cheating evidence.");
                        RequestAction(player, ResponseForName(reason), reason);
                    }
                }
            if(!admissionReady) return;
            while(pending.Count > 0)
            {
                var item=pending.Dequeue();
                var player=AmongUsClient.Instance.FindClientById(item.Owner)?.Character;
                if(now > item.Until || player?.Data == null || player.Data.Disconnected || player.NetId != item.Net || player == PlayerControl.LocalPlayer || item.Owner == AmongUsClient.Instance.HostId || ChatModeration.BoundCode(player) != item.Code) continue;
                // Recheck settings at execution, not just when a filter queued work.
                if(item.Reason == "optional shared-list policy")
                {
                    if(!LobbyChat.WaitingLobby || !CommunityServices.CanEnforce(item.Code,item.Response)) continue;
                }
                else if(item.Reason == "your word filter" || item.Reason == "chat flooding")
                {
                    if(ChatResponse != item.Response || (item.Reason == "your word filter" ? !ChatModeration.WordFilter : !ChatModeration.Spam)) continue;
                }
                else if(item.Reason == "lobby recruitment or invite link")
                { if(!ChatModeration.Ads || AdResponse!=item.Response) continue; }
                else if(item.Reason == "lobby inactivity")
                { if(AfkResponse!=item.Response || !LobbyIdleMonitor.IsIdle(player)) continue; }
                else if(item.Reason == "sustained player proximity")
                { if(ProximityResponse!=item.Response || !ProximityMonitor.Qualifies(player)) continue; }
                else if(!LobbyChat.WaitingLobby || NameReason(player.Data.PlayerName)!=item.Reason || ResponseForName(item.Reason)!=item.Response) continue;
                AmongUsClient.Instance.KickPlayer(item.Owner, item.Response != FilterResponse.Kick);
                AdmissionPacer.Sent(player);
                if(item.Response == FilterResponse.PersistentBan) ModerationStore.Set(item.Code,"ban",true);
                sent=item; sentAt=now; next=now+3;
                Status=$"{item.Response} requested for {PublicText.Name(player.Data.PlayerName)}: {item.Reason}. Moderation preference, not proof of cheating.";
                AUAnticheatPlugin.notifications.Send("Moderation",Status); break;
            }
        }
        public static string NameReason(string name)
        {
            if(ChatModeration.NameFilter && ChatContentRules.MatchesLiteral(name,ModerationStore.Names)) return "name filter";
            if(BotNameFilter && (Beta2Rules.ExactName(name,new[]{"TNT","auser","Haunt Bot"}) || Beta2Rules.ExactName(name,ModerationStore.BotNames))) return "configured bot-name match (not proof of a bot)";
            if(DefaultNameFilter && (BuiltInDefaultNames.IsDefaultName(name) || Beta2Rules.ExactName(name,ModerationStore.DefaultNames))) return "configured default-name match";
            return null;
        }
        public static FilterResponse ResponseForName(string reason) => reason == "configured bot-name match (not proof of a bot)" ? BotNameResponse :
            reason == "configured default-name match" ? DefaultNameResponse : NameResponse;
    }
}
