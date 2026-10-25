using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AUAnticheat.anticheat
{
    // Independent local-only policy store. No community lists, endpoints or tokens.
    internal static class ModerationStore
    {
        public sealed class Entry { public string FriendCode { get; set; } public string Kind { get; set; } }
        private static List<Entry> entries = new();
        private static string directory;
        private static bool loaded;
        public static bool EnforceBans = true;
        public static bool AutoSaveCheaterBans = true;
        public static string Status = "Local moderation lists.";
        public static string[] Words = Array.Empty<string>(), Names = Array.Empty<string>();
        public static string[] BotNames = Array.Empty<string>(), DefaultNames = Array.Empty<string>();
        public static string DirectoryPath => directory ?? "Not initialized";
        public static Entry[] Snapshot() => entries.ToArray();
        public static bool Contains(string code, string kind)
        {
            code = FriendCodeExceptions.Normalize(code);
            return code != null && entries.Exists(e => e.FriendCode == code && e.Kind == kind);
        }
        public static void Initialize(string path) { directory = path; entries = new(); Words = Names = BotNames = DefaultNames = Array.Empty<string>(); loaded = false; Reload(); }
        public static void Reload()
        {
            if(directory == null) return;
            try
            {
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "moderation-list.json");
                var read = new List<Entry>();
                if(File.Exists(path))
                {
                    if(new FileInfo(path).Length > 131072) throw new IOException();
                    read = JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(path)) ?? new();
                }
                if(read.Count > 512 || read.Any(e => e == null || FriendCodeExceptions.Normalize(e.FriendCode) == null || !ValidKind(e.Kind))) throw new FormatException();
                foreach(var entry in read) entry.FriendCode = FriendCodeExceptions.Normalize(entry.FriendCode);
                var words = ReadFilters("filtered-words.txt"); var names = ReadFilters("filtered-names.txt");
                var bots = ReadFilters("bot-names.txt"); var defaults = ReadFilters("default-names.txt");
                entries = read; Words = words; Names = names;
                BotNames = bots; DefaultNames = defaults;
                loaded = true;
                Status = "Lists loaded locally. Watchlist entries are not proof of cheating.";
            }
            catch(Exception) { loaded = false; Status = "Could not load moderation files. Previous lists kept; fix files and reload before saving."; }
        }
        private static string[] ReadFilters(string name)
        {
            string path = Path.Combine(directory, name);
            if(!File.Exists(path)) File.WriteAllText(path, "# One literal word or phrase per line. No regular expressions.\n");
            if(new FileInfo(path).Length > 16384) throw new IOException();
            var lines = File.ReadAllLines(path).Select(s => s.Trim()).Where(s => s.Length > 0 && !s.StartsWith("#")).ToArray();
            if(lines.Length > 128 || lines.Any(s => s.Length > 100)) throw new FormatException();
            return lines;
        }
        private static bool ValidKind(string kind) => kind == "ban" || kind == "block" || kind == "watch";
        public static bool Set(string code, string kind, bool enabled)
        {
            code = FriendCodeExceptions.Normalize(code);
            if(!loaded) { Status = "Fix moderation files and reload before saving; damaged files will not be overwritten."; return false; }
            if(directory == null || code == null || !ValidKind(kind)) { Status = "A matching current friend code is required."; return false; }
            var next = entries.Where(e => !(e.FriendCode == code && e.Kind == kind)).ToList();
            if(enabled) next.Add(new Entry { FriendCode = code, Kind = kind });
            if(next.Count > 512) { Status = "List limit reached (512 entries)."; return false; }
            try
            {
                string path = Path.Combine(directory, "moderation-list.json");
                File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(next, new JsonSerializerOptions { WriteIndented = true }));
                File.Move(path + ".tmp", path, true); entries = next;
                Status = "Saved on this device. No data was uploaded."; return true;
            }
            catch(Exception) { Status = "Save failed. Previous list kept."; return false; }
        }
    }
}
