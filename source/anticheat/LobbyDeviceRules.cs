using System;
using System.Collections.Generic;
using HarmonyLib;
using Hazel;
using UnityEngine;

namespace AUAnticheat.anticheat
{
    // Opt-in modded gameplay, never an anticheat detection. AUR's DevicePatches
    // documents the targeted-comms method (GPL-3.0). Discover console positions
    // from this scene rather than relying on a static map coordinate table.
    internal static class LobbyDeviceRules
    {
        private sealed class Device { public Vector2 Position; public float Radius; public string Rule; }
        private static readonly List<Device> devices=new();
        private static readonly Dictionary<int,uint> muted=new();
        private static int ship;
        private static float next;
        internal static string Classify(string name,bool admin=false)
        {
            if(admin) return "NoAdmin";
            name=(name??"").ToLowerInvariant();
            if(name.Contains("vital")) return "NoVitals";
            if(name.Contains("survlog")) return "NoDoorLogs";
            if(name.Contains("surv") || name.Contains("task_cams")) return "NoCameras";
            return null; // Unknown consoles remain usable; never block ordinary tasks.
        }
        private static bool Disabled(string rule)=>rule!=null && LobbyGameplayRules.Active && LobbyGameplayRules.EnabledRule(rule);
        private static void Discover()
        {
            devices.Clear(); muted.Clear();
            foreach(var console in UnityEngine.Object.FindObjectsOfType<MapConsole>())
                devices.Add(new Device{Position=console.transform.position,Radius=console.UsableDistance+.25f,Rule="NoAdmin"});
            foreach(var console in UnityEngine.Object.FindObjectsOfType<SystemConsole>())
            {
                string rule=Classify(console.name);
                if(rule!=null) devices.Add(new Device{Position=console.transform.position,Radius=console.UsableDistance+.25f,Rule=rule});
            }
        }
        [HarmonyPatch(typeof(SystemConsole),nameof(SystemConsole.Use))]
        private static class LocalDeviceUse { private static bool Prefix(SystemConsole __instance)=>!Disabled(Classify(__instance.name)); }
        [HarmonyPatch(typeof(MapConsole),nameof(MapConsole.Use))]
        private static class LocalAdminUse { private static bool Prefix()=>!Disabled("NoAdmin"); }
        private static bool RealComms()
        {
            if(!ShipStatus.Instance.Systems.TryGetValue(SystemTypes.Comms,out var system)) return true;
            var hq=system.TryCast<HqHudSystemType>(); if(hq!=null) return hq.IsActive;
            var standard=system.TryCast<HudOverrideSystemType>(); return standard==null || standard.IsActive;
        }
        public static void Tick()
        {
            if(ShipStatus.Instance==null || !LobbyGameplayRules.Active) { devices.Clear(); muted.Clear();ship=0;return; }
            int id=ShipStatus.Instance.GetInstanceID(); if(ship!=id) { ship=id;Discover();next=0; }
            float now=Time.realtimeSinceStartup; if(now<next) return;next=now+.5f;
            // A real sabotage always takes precedence. Never send a repair for it.
            if(RealComms()) { muted.Clear(); return; }
            var client=AmongUsClient.Instance; var present=new HashSet<int>();
            foreach(var player in PlayerControl.AllPlayerControls)
            {
                if(player?.Data==null || player.Data.Disconnected || player==PlayerControl.LocalPlayer || player.OwnerId==client.HostId) continue;
                var peer=client.FindClientById(player.OwnerId);
                if(peer==null || peer.Character!=player || peer.IsBeingCreated) continue;
                present.Add(player.OwnerId);
                bool block=false;
                if(!player.Data.IsDead && !player.inVent && MeetingHud.Instance==null && LobbyBehaviour.Instance==null && client.GameState==InnerNet.InnerNetClient.GameStates.Started)
                {
                    var p=player.GetTruePosition();
                    foreach(var device in devices)
                        if(Disabled(device.Rule) && float.IsFinite(p.x) && float.IsFinite(p.y) && Vector2.Distance(p,device.Position)<=device.Radius) { block=true;break; }
                }
                bool previous=muted.TryGetValue(player.OwnerId,out uint net) && net==player.NetId;
                if(block && !previous) { Send(player,128);muted[player.OwnerId]=player.NetId; }
                else if(!block && previous)
                {
                    Send(player,16);
                    int map=GameOptionsManager.Instance.CurrentGameOptions.MapId;
                    if(map==1 || map==5) Send(player,17);
                    muted.Remove(player.OwnerId);
                }
            }
            foreach(int owner in new List<int>(muted.Keys)) if(!present.Contains(owner)) muted.Remove(owner);
        }
        private static void Send(PlayerControl player,byte amount)
        {
            var writer=AmongUsClient.Instance.StartRpcImmediately(ShipStatus.Instance.NetId,(byte)RpcCalls.UpdateSystem,SendOption.Reliable,player.OwnerId);
            writer.Write((byte)SystemTypes.Comms); writer.WritePacked(player.NetId);writer.Write(amount);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
    }
}
