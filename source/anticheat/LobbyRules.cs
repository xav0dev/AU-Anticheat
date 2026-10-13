using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AUAnticheat.anticheat
{
    // User-authored rules, stored locally. Only ordinary, paced host chat is used.
    internal static class LobbyRules
    {
        private static bool enabled;
        private static int delay = 10;
        private static Action<bool, int> saveSettings;
        private static string[] lines = Array.Empty<string>();
        private static string fullSession = "";
        private static float fullSince;
        private static bool posted;
        private static string sentFullSession = "";
        public static void NewRound() { sentFullSession = ""; Reset(); }
        public static void Disconnect() { sentFullSession = ""; Reset(); }
        public static void MarkSent() { sentFullSession = fullSession; Status = "Rules sent once for this full lobby. Waiting for the next refill."; }
        public static string FilePath { get; private set; } = "Not initialized";
        public static string Status { get; private set; } = "No rules loaded.";
        public static string Preview => string.Join("\n", lines);
        public static string MessageInfo
        {
            get
            {
                int length = LobbyChat.CombinedMessage(lines).Length;
                return length > LobbyChat.MaximumPublicMessageLength ?
                    $"Too long: {length}/{LobbyChat.MaximumPublicMessageLength} characters including separators. Shorten the rules; nothing will be posted. Your saved text is kept." :
                    $"One chat message: {length}/{LobbyChat.MaximumPublicMessageLength} characters including separators. No label is added.";
            }
        }
        public static string DraftMessageInfo(string text)
        {
            var draft = new List<string>();
            foreach(string raw in (text ?? "").Replace("\r", "").Split('\n'))
            {
                string line = raw.Trim();
                if(line.Length > 0 && !line.StartsWith("#")) draft.Add(line);
            }
            int length = LobbyChat.CombinedMessage(draft).Length;
            return $"One message: {length}/100 characters including separators. No label is added." +
                (length > 100 ? " Too long to save; shorten the rules." : "");
        }
        public static bool SaveText(string text)
        {
            if(text == null || System.Text.Encoding.UTF8.GetByteCount(text)>4096) { Status="Rules too large (maximum 4 KB). Nothing was saved."; return false; }
            int count=0;
            var accepted = new List<string>();
            foreach(string raw in text.Replace("\r","").Split('\n'))
            {
                string line=raw.Trim(); if(line.Length==0||line.StartsWith("#")) continue;
                if(line.Length>100||++count>8) { Status="Use up to 8 short rules fitting one 100-character message. Nothing was saved."; return false; }
                accepted.Add(line);
            }
            if(LobbyChat.CombinedMessage(accepted).Length > LobbyChat.MaximumPublicMessageLength)
            { Status="Rules exceed one 100-character message including separators. Nothing was saved."; return false; }
            try
            {
                File.WriteAllText(FilePath+".tmp",text.Replace("\r",""));
                File.Move(FilePath+".tmp",FilePath,true);
                Reload(); return true;
            }
            catch(Exception) { Status="Could not save rules. Your previous file was kept; check folder permissions."; return false; }
        }
        public static bool Enabled
        {
            get => enabled;
            set { if(enabled == value) return; enabled = value; Reset(); Save(); }
        }
        public static int DelaySeconds
        {
            get => delay;
            set { value = Math.Clamp(value, 1, 120); if(delay == value) return; delay = value; Reset(); Save(); }
        }
        private static void Save()
        {
            try { saveSettings?.Invoke(enabled, delay); }
            catch(Exception) { Status = "Could not save settings. Check folder permissions."; }
        }
        public static void Initialize(string directory, bool use, int seconds, Action<bool,int> persist)
        {
            enabled = use; delay = Math.Clamp(seconds,1,120); saveSettings = persist; Reset();
            FilePath = Path.Combine(directory, "lobby-rules.txt");
            try
            {
                Directory.CreateDirectory(directory);
                // Never overwrite an existing rules list.
                if(!File.Exists(FilePath)) File.WriteAllText(FilePath,
                    "# Edit here or use the rules editor under Lobby in the menu.\n" +
                    "# All rules share one message: 100 characters including separators; no label added. Lines starting # are comments.\n" +
                    "Be respectful. No cheating or harassment.\n");
                Reload();
            }
            catch(Exception) { lines = Array.Empty<string>(); Status = "Rules file could not be opened. Check folder permissions."; }
        }
        public static void Reload()
        {
            Reset(); lines = Array.Empty<string>();
            try
            {
                using var file = new FileStream(FilePath,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);
                if(file.Length > 4096) { Status = "Rules file is too large. Maximum: 4 KB."; return; }
                using var reader = new StreamReader(file);
                var accepted = new List<string>();
                int chars = 0;
                while(!reader.EndOfStream)
                {
                    string line = reader.ReadLine() ?? "";
                    chars += line.Length + 1;
                    if(chars > 4096) { Status = "Rules file is too large. Maximum: 4 KB."; return; }
                    line = line.Trim();
                    if(line.Length == 0 || line.StartsWith("#")) continue;
                    if(line.Length > 100 || accepted.Count == 8) { Status = "Use up to 8 short rules fitting one message. Nothing was queued. Existing file kept."; return; }
                    string safe = PublicText.Clean(line,int.MaxValue);
                    if(!string.IsNullOrWhiteSpace(safe)) accepted.Add(safe);
                }
                lines = accepted.ToArray();
                Status = lines.Length == 0 ? "No rules loaded. Edit the file and reload." : $"{lines.Length} rules loaded. Saved on this device.";
            }
            catch(Exception) { Status = "Could not read rules. Check the file and click Reload saved rules."; }
        }
        private static string FullLobbyKey()
        {
            var client = AmongUsClient.Instance;
            if(!enabled || !AUAnticheatPlugin.ProtectionsAttached || client == null || !client.AmHost ||
               LobbyBehaviour.Instance == null || MeetingHud.Instance != null || client.GameState != InnerNet.InnerNetClient.GameStates.Joined ||
               (GameManager.Instance != null && GameManager.Instance.GameHasStarted)) return "";
            var ids = new HashSet<int>(); var roster = new List<string>();
            foreach(var player in PlayerControl.AllPlayerControls)
            {
                if(player == null || player.Data == null || player.Data.Disconnected) continue;
                var peer = client.FindClientById(player.OwnerId);
                if(peer == null || peer.IsBeingCreated || peer.Character != player) continue;
                if(ids.Add(player.OwnerId)) roster.Add($"{player.OwnerId}:{player.NetId}");
            }
            int capacity = GameOptionsManager.Instance?.CurrentGameOptions?.MaxPlayers ?? 0;
            if(capacity < 4 || capacity > 15 || ids.Count != capacity) return "";
            roster.Sort(StringComparer.Ordinal);
            return $"{client.GameId}:{client.HostId}:{LobbyBehaviour.Instance.GetInstanceID()}:{capacity}:" + string.Join(",",roster);
        }
        public static bool CanSend => enabled && fullSession.Length > 0 && fullSession != sentFullSession && fullSession == FullLobbyKey();
        public static void Reset() { if(posted) Status = "Waiting for a stable full lobby."; fullSession = ""; posted = false; LobbyChat.CancelRules(); }
        public static void Tick()
        {
            string current = FullLobbyKey();
            if(current.Length == 0)
            {
                // Leaving a full lobby rearms even if the same player later returns.
                // Toggling automation off/on alone must not duplicate a sent post.
                if(enabled) sentFullSession = "";
                Reset(); return;
            }
            if(current != fullSession)
            {
                if(current != sentFullSession) sentFullSession = "";
                Reset(); fullSession = current; fullSince = Time.realtimeSinceStartup;
            }
            if(LobbyChat.CombinedMessage(lines).Length > LobbyChat.MaximumPublicMessageLength) { Status = MessageInfo; return; }
            if(posted || sentFullSession == current || lines.Length == 0 || Time.realtimeSinceStartup - fullSince < delay) return;
            if(LobbyChat.QueueRules(lines)) { posted = true; Status = "One rules message queued for this fill. Waiting for the normal chat cooldown."; }
        }
    }
}
