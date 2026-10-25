using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Hazel;
using UnityEngine;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace AUAnticheat.anticheat
{
    // Independent implementation of selected classic-lobby controls reviewed in
    // AmongUsRevamped. Only an explicitly registered modded host applies them.
    internal static class LobbyGameplayRules
    {
        public sealed class Rule
        {
            public readonly string Key,Label; public readonly float Min,Max,Default; public readonly bool Toggle;
            public float Value;
            public Rule(string key,string label,float min,float max,float value,bool toggle=false)
            { Key=key;Label=label;Min=min;Max=max;Value=Default=value;Toggle=toggle; }
        }
        public static readonly Rule[] Options={
            new("NoCameras","Block camera consoles",0,1,0,true), new("NoVitals","Block vitals consoles",0,1,0,true),
            new("NoAdmin","Block admin tables",0,1,0,true), new("NoDoorLogs","Block MIRA door logs",0,1,0,true),
            new("DeadSabotage","Dead impostors can sabotage",0,1,1,true),
            new("NoDoors","Disable door sabotages",0,1,0,true),
            new("NoReactor","Disable reactor / laboratory sabotage",0,1,0,true),
            new("NoOxygen","Disable oxygen sabotage",0,1,0,true),
            new("NoLights","Disable lights sabotage",0,1,0,true),
            new("NoComms","Disable communications sabotage",0,1,0,true),
            new("NoHeli","Disable Airship crash-course sabotage",0,1,0,true),
            new("NoMixup","Disable mushroom mixup sabotage",0,1,0,true),
            new("ButtonDelay","Block emergency buttons for first N seconds",0,120,0),
            new("CustomSabotage","Customize sabotage timers",0,1,0,true),
            new("SabotageCooldown","Sabotage cooldown",5,180,30),
            new("Reactor0","Skeld reactor duration",10,180,30), new("Reactor1","MIRA reactor duration",10,180,45),
            new("Reactor2","Polus reactor duration",10,180,60), new("Reactor5","Fungle reactor duration",10,180,60),
            new("Oxygen0","Skeld oxygen duration",10,180,30), new("Oxygen1","MIRA oxygen duration",10,180,45),
            new("Heli","Airship crash-course duration",10,180,90), new("Mixup","Mushroom mixup duration",5,120,10),
            new("CustomDecon","Customize decontamination timers",0,1,0,true),
            new("Decon1","MIRA decontamination time",.5f,10,3), new("Door1","MIRA decon door-open time",.5f,10,3),
            new("Decon2","Polus decontamination time",.5f,10,3), new("Door2","Polus decon door-open time",.5f,10,3),
            new("SameTasks","Give the same assigned tasks to players",0,1,0,true),
            new("TaskPercent","Task completion required for crew win (%)",30,100,100)
        };
        public static readonly HashSet<string> DisabledTasks=new(StringComparer.Ordinal);
        private static Dictionary<string,float> active=new();
        private static HashSet<string> activeTasks=new();
        private static int gameId,hostId,shipId;
        private static float started;
        private static byte[] sharedTasks;
        private static bool taskWinSent;
        public static string Status="Draft only. Use Apply extended settings before starting a modded lobby.";
        public static bool SetDraft(string key,string text)
        {
            var item=Array.Find(Options,x=>x.Key==key);
            if(item==null || !float.TryParse(text.Replace(',','.'),System.Globalization.NumberStyles.AllowDecimalPoint,
                System.Globalization.CultureInfo.InvariantCulture,out float value) || !float.IsFinite(value) || value<item.Min || value>item.Max || (item.Toggle && value!=0 && value!=1)) return false;
            item.Value=value; return true;
        }
        public static void ClearActive() { active.Clear(); activeTasks.Clear(); sharedTasks=null; shipId=0; }
        public static void ApplyDraft()
        {
            if(!ValidDraft()) throw new InvalidOperationException("Invalid gameplay rule draft.");
            active=Options.ToDictionary(x=>x.Key,x=>x.Value); activeTasks=new(DisabledTasks,StringComparer.Ordinal);
            gameId=AmongUsClient.Instance.GameId; hostId=AmongUsClient.Instance.HostId; shipId=0; sharedTasks=null;
            Status="Host gameplay rules applied for this modded lobby. Tell players the rules before starting.";
        }
        public static bool ValidDraft()=>Options.All(x=>float.IsFinite(x.Value) && x.Value>=x.Min && x.Value<=x.Max && (!x.Toggle || x.Value==0 || x.Value==1));
        internal static bool Active => CustomLobbySettings.Enabled && Constants.IsVersionModded() && AmongUsClient.Instance!=null &&
            AmongUsClient.Instance.AmHost && AmongUsClient.Instance.ClientId==AmongUsClient.Instance.HostId &&
            AmongUsClient.Instance.GameId==gameId && AmongUsClient.Instance.HostId==hostId && active.Count>0;
        private static float Value(string key,float fallback=0)=>active.TryGetValue(key,out float value)?value:fallback;
        private static bool On(string key)=>Value(key)==1;
        public static bool EnabledRule(string key)=>On(key);
        private static int Map=>GameOptionsManager.Instance.CurrentGameOptions.MapId;
        private static void Round()
        {
            int id=ShipStatus.Instance==null?0:ShipStatus.Instance.GetInstanceID();
            if(id==shipId) return; shipId=id; started=Time.realtimeSinceStartup; sharedTasks=null; taskWinSent=false; Timers.Clear();
        }
        public static void Tick()
        {
            if(!Active || ShipStatus.Instance==null || LobbyBehaviour.Instance!=null) return;
            Round();
            if(Value("TaskPercent",100)>=100 || taskWinSent || Time.realtimeSinceStartup-started<15 ||
                AmongUsClient.Instance.GameState!=InnerNet.InnerNetClient.GameStates.Started || MeetingHud.Instance!=null || GameData.Instance==null) return;
            int total=GameData.Instance.TotalTasks,done=GameData.Instance.CompletedTasks;
            if(total>0 && done>=Math.Ceiling(total*Value("TaskPercent",100)/100d))
            { taskWinSent=true; GameManager.Instance.RpcEndGame(GameOverReason.CrewmatesByTask,false); }
        }
        [HarmonyPatch(typeof(PlayerControl),nameof(PlayerControl.ReportDeadBody))]
        private static class ModdedButtonRule
        {
            private static bool Prefix(NetworkedPlayerInfo __0)
            { if(!Active || __0!=null || ShipStatus.Instance==null) return true; Round(); return Time.realtimeSinceStartup-started>=Value("ButtonDelay"); }
        }
        [HarmonyPatch(typeof(ShipStatus),nameof(ShipStatus.CloseDoorsOfType))]
        private static class ModdedDoorRule { private static bool Prefix()=>!Active || !On("NoDoors"); }
        [HarmonyPatch(typeof(SabotageSystemType),nameof(SabotageSystemType.UpdateSystem))]
        private static class ModdedSabotageRule
        {
            private static bool Prefix(SabotageSystemType __instance,PlayerControl __0,MessageReader __1,ref bool __state)
            {
                if(!Active || __1==null || __0?.Data==null) return true;
                var reader=MessageReader.Get(__1);
                try {
                    if(reader.BytesRemaining<1) return true;
                    var system=(SystemTypes)reader.ReadByte();
                    string key=system switch { SystemTypes.Reactor or SystemTypes.Laboratory=>"NoReactor",SystemTypes.LifeSupp=>"NoOxygen",
                        SystemTypes.Electrical=>"NoLights",SystemTypes.Comms=>"NoComms",SystemTypes.HeliSabotage=>"NoHeli",SystemTypes.MushroomMixupSabotage=>"NoMixup",_=>"" };
                    if(On(key) || (__0.Data.IsDead && !On("DeadSabotage"))) return false;
                    __state=On("CustomSabotage"); return true;
                } finally { reader.Recycle(); }
            }
            private static void Postfix(SabotageSystemType __instance,bool __state)
            { if(__state && Active) { __instance.Timer=Value("SabotageCooldown",30); __instance.IsDirty=true; } }
        }
        private static readonly HashSet<string> Timers=new();
        private static bool First(string key,bool running)
        {
            Round(); if(!running) { Timers.Remove(key); return false; }
            return Timers.Add(key);
        }
        [HarmonyPatch(typeof(ReactorSystemType),nameof(ReactorSystemType.Deteriorate))]
        private static class ModdedReactorTimer
        { private static void Prefix(ReactorSystemType __instance) { if(Active && On("CustomSabotage") && First("reactor",__instance.IsActive)) { __instance.Countdown=Value("Reactor"+(Map==3?0:Map),60); __instance.IsDirty=true; } } }
        [HarmonyPatch(typeof(LifeSuppSystemType),nameof(LifeSuppSystemType.Deteriorate))]
        private static class ModdedOxygenTimer
        { private static void Prefix(LifeSuppSystemType __instance) { if(Active && On("CustomSabotage") && First("oxygen",__instance.IsActive)) { __instance.Countdown=Value("Oxygen"+(Map==3?0:Map),30); __instance.IsDirty=true; } } }
        [HarmonyPatch(typeof(HeliSabotageSystem),nameof(HeliSabotageSystem.Deteriorate))]
        private static class ModdedHeliTimer
        { private static void Prefix(HeliSabotageSystem __instance) { if(Active && On("CustomSabotage") && First("heli",__instance.IsActive)) { __instance.Countdown=Value("Heli",90); __instance.IsDirty=true; } } }
        [HarmonyPatch(typeof(MushroomMixupSabotageSystem),nameof(MushroomMixupSabotageSystem.Deteriorate))]
        private static class ModdedMixupTimer
        { private static void Prefix(MushroomMixupSabotageSystem __instance) { if(Active && On("CustomSabotage") && First("mixup",__instance.IsActive)) { __instance.currentSecondsUntilHeal=Value("Mixup",10); __instance.IsDirty=true; } } }
        [HarmonyPatch(typeof(DeconSystem),nameof(DeconSystem.UpdateSystem))]
        private static class ModdedDeconTimer
        { private static void Prefix(DeconSystem __instance) { if(Active && On("CustomDecon") && (Map==1 || Map==2)) { __instance.DeconTime=Value("Decon"+Map,3); __instance.DoorOpenTime=Value("Door"+Map,3); } } }
        [HarmonyPatch(typeof(ShipStatus),nameof(ShipStatus.AddTasksFromList))]
        private static class ModdedTaskPool
        {
            private static void Prefix(Il2CppSystem.Collections.Generic.List<NormalPlayerTask> __4)
            {
                if(!Active || __4==null || activeTasks.Count==0) return;
                var remove=new List<NormalPlayerTask>();
                foreach(var task in __4) if(task!=null && activeTasks.Contains(task.TaskType.ToString())) remove.Add(task);
                if(remove.Count==__4.Count) { Status="A task pool would be empty; its filter was skipped. Leave at least one task of each requested length enabled."; return; }
                foreach(var task in remove) __4.Remove(task);
            }
        }
        [HarmonyPatch(typeof(NetworkedPlayerInfo),nameof(NetworkedPlayerInfo.RpcSetTasks))]
        private static class ModdedSameTasks
        {
            private static void Prefix(ref Il2CppStructArray<byte> __0)
            {
                if(!Active || !On("SameTasks") || __0==null || __0.Length==0) return;
                Round();
                if(sharedTasks==null) { sharedTasks=new byte[__0.Length]; for(int i=0;i<__0.Length;i++) sharedTasks[i]=__0[i]; }
                else __0=new Il2CppStructArray<byte>(sharedTasks);
            }
        }
    }
}
