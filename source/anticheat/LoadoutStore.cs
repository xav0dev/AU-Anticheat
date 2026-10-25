using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AUAnticheat.anticheat
{
    // Single bounded file; user names never become filesystem paths.
    internal sealed class LoadoutStore
    {
        public sealed class Entry
        {
            public string Build { get; set; }
            public string Payload { get; set; }
        }
        private readonly string path;
        private Dictionary<string,Entry> entries = new(StringComparer.OrdinalIgnoreCase);
        public string Status { get; private set; } = "No saved loadouts.";
        public LoadoutStore(string directory)
        {
            path = Path.Combine(directory,"lobby-loadouts.json");
            try
            {
                if(!File.Exists(path)) return;
                if(new FileInfo(path).Length > 262144) throw new FormatException();
                var loaded = JsonSerializer.Deserialize<Dictionary<string,Entry>>(File.ReadAllText(path));
                if(loaded == null || loaded.Count > 20) throw new FormatException();
                foreach(var pair in loaded)
                {
                    if(!ValidName(pair.Key) || pair.Value == null || string.IsNullOrEmpty(pair.Value.Build) || pair.Value.Build.Length > 100 ||
                       pair.Value.Payload == null || pair.Value.Payload.Length > 5464 || Convert.FromBase64String(pair.Value.Payload).Length is < 1 or > 4096)
                        throw new FormatException();
                    entries.Add(pair.Key,pair.Value);
                }
                Status = "Loadouts loaded from this device.";
            }
            catch(Exception) { entries.Clear(); Status = "Could not load saved loadouts. The existing file was not changed."; readFailed = true; }
        }
        private bool readFailed;
        public static bool ValidName(string name) => !string.IsNullOrWhiteSpace(name) && name == name.Trim() && name.Length <= 32 &&
            name.All(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_');
        public string[] Names => entries.Keys.OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).ToArray();
        public Entry Find(string name) => name != null && entries.TryGetValue(name,out var value) ? value : null;
        public bool Save(string name,string build,byte[] bytes)
        {
            if(!ValidName(name) || bytes == null || bytes.Length == 0 || bytes.Length > 4096 || string.IsNullOrEmpty(build) || build.Length > 100)
            { Status = "Use a name of 1–32 letters, numbers, spaces, hyphens or underscores."; return false; }
            if(entries.ContainsKey(name)) { Status = "That name exists. Use a different name or delete the old loadout first."; return false; }
            if(entries.Count >= 20) { Status = "Maximum 20 loadouts. Delete one before saving another."; return false; }
            var next = new Dictionary<string,Entry>(entries,StringComparer.OrdinalIgnoreCase) { [name] = new Entry { Build=build,Payload=Convert.ToBase64String(bytes) } };
            return Write(next,"Loadout saved.");
        }
        public bool Delete(string name)
        {
            if(name == null || !entries.ContainsKey(name)) return false;
            var next = new Dictionary<string,Entry>(entries,StringComparer.OrdinalIgnoreCase); next.Remove(name);
            return Write(next,"Loadout deleted.");
        }
        private bool Write(Dictionary<string,Entry> next,string status)
        {
            if(readFailed) { Status = "Saved file needs repair before changes can be saved; it has not been overwritten."; return false; }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path+".tmp",JsonSerializer.Serialize(next,new JsonSerializerOptions { WriteIndented=true }));
                File.Move(path+".tmp",path,true); entries=next; Status=status; return true;
            }
            catch(Exception) { Status="Could not save loadouts. Previous file was kept."; return false; }
        }
    }
}
