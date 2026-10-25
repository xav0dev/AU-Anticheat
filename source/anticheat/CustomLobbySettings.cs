using System;
using System.Collections.Generic;
using System.Globalization;
using AmongUs.GameOptions;
using HarmonyLib;
using UnityEngine;

namespace AUAnticheat.anticheat
{
    // Explicitly registered host-only modded mode. Never changes a connected
    // vanilla lobby into a modded lobby, patches validation away, or alters capacity.
    internal static class CustomLobbySettings
    {
        public static bool Enabled { get; private set; }
        public static string Status="Extended settings require modded mode selected BEFORE hosting.";
        private static float nextApply;
        public sealed class Option
        {
            public string Name, Label; public bool Integer, Boolean; public float Min, Max, Value; public readonly float DefaultValue;
            public Option(string name,string label,bool integer,float min,float max,float value,bool boolean=false) { Name=name;Label=label;Integer=integer;Min=min;Max=max;Value=DefaultValue=value;Boolean=boolean; }
        }
        public static readonly Option[] Options = {
            new("PlayerSpeedMod","Player speed",false,.25f,5,1), new("CrewLightMod","Crew vision",false,.25f,5,1),
            new("ImpostorLightMod","Impostor vision",false,.25f,5,1.5f), new("KillCooldown","Kill cooldown",false,0,180,30),
            new("EmergencyCooldown","Emergency cooldown",true,0,180,15), new("DiscussionTime","Discussion time",true,0,600,15),
            new("VotingTime","Voting time (0 = unlimited)",true,0,600,120),
            new("EngineerCooldown","Engineer cooldown",false,0,180,30), new("EngineerInVentMaxTime","Engineer vent time",false,1,180,15),
            new("ShapeshifterCooldown","Shapeshifter cooldown",false,0,180,30), new("ShapeshifterDuration","Shapeshifter duration",false,1,180,15),
            new("ScientistCooldown","Scientist cooldown",false,0,180,15), new("ScientistBatteryCharge","Scientist battery",false,1,180,5),
            new("GuardianAngelCooldown","Guardian Angel cooldown",false,0,180,60), new("ProtectionDurationSeconds","Protection duration",false,1,60,10),
            new("PhantomCooldown","Phantom cooldown",false,0,180,30), new("PhantomDuration","Phantom duration",false,1,180,15),
            new("TrackerCooldown","Tracker cooldown",false,0,180,30), new("TrackerDuration","Tracker duration",false,1,180,30),
            new("TrackerDelay","Tracker update delay",false,.25f,60,1),
            new("NumImpostors","Impostors",true,1,3,1), new("NumEmergencyMeetings","Emergency meetings per player",true,0,100,1),
            new("KillDistance","Kill distance (0 short, 1 medium, 2 long)",true,0,2,1),
            new("NumCommonTasks","Common tasks",true,0,10,1), new("NumShortTasks","Short tasks",true,0,30,2), new("NumLongTasks","Long tasks",true,0,15,1),
            new("TaskBarMode","Task bar (0 always, 1 meetings, 2 never)",true,0,2,0),
            new("NoisemakerAlertDuration","Noisemaker alert duration",false,1,180,10),
            new("ViperDissolveTime","Viper dissolve time",false,1,180,15), new("DetectiveSuspectLimit","Detective suspect limit",false,1,15,3),
            new("JudgeTaskRequirementPercentage","Judge task requirement (%)",false,0,100,100),
            new("SpiritGuideCooldownSeconds","Influencer cooldown",false,0,180,30),
            new("VisualTasks","Visual tasks",true,0,1,1,true), new("ConfirmImpostor","Confirm ejects",true,0,1,1,true),
            new("AnonymousVotes","Anonymous votes",true,0,1,0,true), new("GhostsDoTasks","Ghosts do tasks",true,0,1,1,true),
            new("ShapeshifterLeaveSkin","Shapeshifter leaves evidence",true,0,1,1,true),
            new("ImpostorsCanSeeProtect","Impostors see protection",true,0,1,0,true), new("NoisemakerImpostorAlert","Impostors hear Noisemaker",true,0,1,0,true)
        };
        public static bool SetMode(bool value)
        {
            if(AmongUsClient.Instance != null && (AmongUsClient.Instance.AmConnected || AmongUsClient.Instance.isConnecting))
            { Status="Leave the server before changing lobby mode. Existing lobbies are never silently converted."; return false; }
            Enabled=value; if(!value) LobbyGameplayRules.ClearActive(); Status=value ? "Modded +25 mode selected. Host a NEW lobby and tell players about custom settings." : "Normal defensive mode selected."; return true;
        }
        public static bool SetDraft(string name,string text)
        {
            var item=Array.Find(Options,x=>x.Name==name);
            if(item==null || text==null || text.Length>24 ||
               !float.TryParse(text.Trim().Replace(',','.'),NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out float value) ||
               !float.IsFinite(value) || value<item.Min || value>item.Max || (item.Integer && value!=MathF.Truncate(value)))
            { Status="Enter a number within the displayed range; whole numbers for meeting times. Previous value kept."; return false; }
            item.Value=value; Status="Draft updated. Use Apply to send the complete draft to your modded lobby."; return true;
        }
        [HarmonyPatch(typeof(Constants),nameof(Constants.GetBroadcastVersion))]
        private static class RegisterHostMode { private static void Postfix(ref int __result) { if(Enabled) __result=checked(__result+25); } }
        [HarmonyPatch(typeof(Constants),nameof(Constants.IsVersionModded))]
        private static class IdentifyHostMode { private static void Postfix(ref bool __result) { if(Enabled) __result=true; } }
        public static void ReadCurrent()
        {
            var options=GameOptionsManager.Instance?.CurrentGameOptions;
            if(options==null) { Status="Join your lobby first."; return; }
            foreach(var item in Options)
            {
                try { item.Value=Read(options,item); }
                catch(Exception) { /* Unsupported role option remains in draft; apply will reject it. */ }
            }
            Status="Copied current values into the draft. Nothing has been applied.";
        }
        public static bool Apply()
        {
            if(!LobbyGameplayRules.ValidDraft()) { Status="A gameplay rule is outside its supported range. No settings sent."; return false; }
            if(!Enabled || !LobbyChat.WaitingLobby || !Constants.IsVersionModded() || GameManager.Instance?.LogicOptions==null)
            { Status="Host a waiting modded lobby first. Normal lobbies cannot receive extended settings."; return false; }
            if(Time.realtimeSinceStartup<nextApply) { Status="Wait five seconds between settings updates."; return false; }
            var manager=GameOptionsManager.Instance; var logic=GameManager.Instance.LogicOptions;
            var previous=manager.CurrentGameOptions; var oldHost=manager.GameHostOptions; bool changed=false;
            try
            {
                var options=logic.gameOptionsFactory.FromBytes(logic.gameOptionsFactory.ToBytes(previous,false));
                foreach(var item in Options)
                {
                    if(!float.IsFinite(item.Value) || item.Value<item.Min || item.Value>item.Max || (item.Integer && item.Value!=MathF.Truncate(item.Value))) { Status="Draft contains an out-of-range value: "+item.Label; return false; }
                    if(item.Boolean) options.SetBool(Enum.Parse<BoolOptionNames>(item.Name),item.Value==1);
                    else if(item.Integer) options.SetInt(Enum.Parse<Int32OptionNames>(item.Name),(int)Math.Round(item.Value));
                    else options.SetFloat(Enum.Parse<FloatOptionNames>(item.Name),item.Value);
                }
                // A standard/recommended preset can replace custom values later.
                var normal=options.Cast<NormalGameOptionsV12>();
                normal.RulesPreset=RulesPresets.Custom;
                options.SetBool(BoolOptionNames.IsDefaults,false);
                // Verify the serialized representation before touching live state.
                var roundTrip=logic.gameOptionsFactory.FromBytes(logic.gameOptionsFactory.ToBytes(options,false));
                foreach(var item in Options)
                {
                    float actual=Read(roundTrip,item);
                    if(!float.IsFinite(actual) || Math.Abs(actual-item.Value)>.0001f)
                    { Status="Game did not retain "+item.Label+". No settings were sent. Try a supported value."; return false; }
                }
                nextApply=Time.realtimeSinceStartup+5; changed=true;
                manager.CurrentGameOptions=options; manager.GameHostOptions=options;
                logic.SetGameOptions(options); logic.SyncOptions(); LobbyRules.Reset(); LobbyGameplayRules.ApplyDraft();
                Status="Requested extended settings via normal option sync. Verify settings with players; server/client limits may still apply."; return true;
            }
            catch(Exception)
            {
                if(changed) try { manager.CurrentGameOptions=previous; manager.GameHostOptions=oldHost; logic.SetGameOptions(previous); logic.SyncOptions(); } catch(Exception) { }
                Status="Settings were unsupported or sync failed. Check the lobby before starting; a partial update may have occurred."; return false;
            }
        }
        private static float Read(IGameOptions options,Option item) => item.Boolean ? (options.GetBool(Enum.Parse<BoolOptionNames>(item.Name))?1:0) :
            item.Integer ? options.GetInt(Enum.Parse<Int32OptionNames>(item.Name)) : options.GetFloat(Enum.Parse<FloatOptionNames>(item.Name));
        public static bool ValidSerialized(IGameOptions options)
        {
            if(!Enabled || !Constants.IsVersionModded()) return false;
            try { return Array.TrueForAll(Options,item=> { float value=Read(options,item); return float.IsFinite(value) && value>=item.Min && value<=item.Max && (!item.Integer || value==MathF.Truncate(value)); }); }
            catch(Exception) { return false; }
        }
    }
}
