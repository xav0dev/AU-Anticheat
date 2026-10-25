using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AUAnticheat.anticheat
{
    internal static class WardrobeStore
    {
        internal sealed class Outfit
        {
            public string Name {get;set;} = "";
            public string Hat {get;set;} = "";
            public string Skin {get;set;} = "";
            public string Visor {get;set;} = "";
            public string Pet {get;set;} = "";
            public string NamePlate {get;set;} = "";
            public int Color {get;set;}
        }
        private static List<Outfit> outfits=new();
        private static string path;
        private static bool loaded;
        public static string Status="Save outfits from your own wardrobe. No cosmetic unlocks.";
        public static Outfit[] Snapshot()=>outfits.Select(Clone).ToArray();
        private static Outfit Clone(Outfit x)=>new(){Name=x.Name,Hat=x.Hat,Skin=x.Skin,Visor=x.Visor,Pet=x.Pet,NamePlate=x.NamePlate,Color=x.Color};
        private static bool Valid(Outfit x)=>x!=null && x.Name!=null && x.Name.Length is >0 and <=32 &&
            x.Name.All(c=>!char.IsControl(c) && c!='<' && c!='>') && x.Color>=0 && x.Color<18 &&
            new[]{x.Hat,x.Skin,x.Visor,x.Pet,x.NamePlate}.All(s=>s!=null && s.Length<=160 && s.All(c=>!char.IsControl(c)));
        public static void Initialize(string directory)
        {
            path=Path.Combine(directory,"wardrobe.json"); outfits=new(); loaded=false;
            try {
                if(!File.Exists(path)) { loaded=true; return; }
                if(new FileInfo(path).Length>131072) throw new IOException();
                var items=JsonSerializer.Deserialize<List<Outfit>>(File.ReadAllText(path));
                if(items==null || items.Count>24 || items.Any(x=>!Valid(x)) || items.Select(x=>x.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=items.Count) throw new FormatException();
                outfits=items; loaded=true; Status=$"Loaded {items.Count} saved outfits.";
            } catch(Exception) { Status="Could not load wardrobe.json. File preserved; check its format."; }
        }
        private static bool Write(List<Outfit> items)
        {
            try { if(path==null || !loaded) { Status="Fix the saved wardrobe file and restart before saving. File was not overwritten."; return false; } Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path+".tmp",JsonSerializer.Serialize(items,new JsonSerializerOptions{WriteIndented=true}));
                File.Move(path+".tmp",path,true); outfits=items; return true;
            } catch(Exception error) { Status="Outfits could not be saved ("+error.GetType().Name+"). Check folder permissions."; return false; }
        }
        public static bool Add(Outfit outfit)
        {
            if(!Valid(outfit) || outfits.Count>=24 || outfits.Any(x=>string.Equals(x.Name,outfit.Name,StringComparison.OrdinalIgnoreCase)))
            { Status="Use a unique name (1–32 characters). Maximum 24 outfits. Existing outfits are never overwritten."; return false; }
            var copy=outfits.Select(Clone).ToList(); copy.Add(Clone(outfit));
            if(!Write(copy)) return false; Status="Outfit saved locally."; return true;
        }
        public static bool Delete(string name)
        {
            var copy=outfits.Where(x=>x.Name!=name).Select(Clone).ToList();
            if(copy.Count==outfits.Count || !Write(copy)) return false;
            Status="Saved outfit removed; your worn cosmetics are unchanged."; return true;
        }
    }
}
