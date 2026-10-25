using System;
using System.Linq;
using UnityEngine;
using AmongUs.Data;
using HarmonyLib;
using System.Collections.Generic;

namespace AUAnticheat.anticheat
{
    internal static class Wardrobe
    {
        private static float nextApply;
        private static float nextColor;
        private static int previewColor=-1;
        public static int PreferredColor=-1;
        public static bool RestorePreferredColor=true;
        [HarmonyPatch(typeof(PlayerTab),nameof(PlayerTab.SelectColor))]
        private static class SelectedWardrobeColor
        {
            private static void Postfix(PlayerTab __instance)
            { previewColor=__instance.currentColor; PreferredColor=-1; }
        }
        public static string Status="Use at the main menu or in a waiting lobby.";
        private static bool CanEdit()
        {
            var client=AmongUsClient.Instance;
            return client==null || (!client.AmConnected && !client.isConnecting) ||
                (LobbyBehaviour.Instance!=null && client.GameState==InnerNet.InnerNetClient.GameStates.Joined &&
                 PlayerControl.LocalPlayer?.Data!=null && !PlayerControl.LocalPlayer.Data.Disconnected &&
                 GameStartManager.Instance?.startState==GameStartManager.StartingStates.NotStarting);
        }
        private static bool Owned(WardrobeStore.Outfit x)
        {
            var hats=HatManager.Instance;
            return hats!=null && x.Color>=0 && x.Color<Palette.PlayerColors.Length &&
                hats.GetUnlockedHats().Any(i=>i.ProductId==x.Hat) &&
                hats.GetUnlockedSkins().Any(i=>i.ProductId==x.Skin) &&
                hats.GetUnlockedVisors().Any(i=>i.ProductId==x.Visor) &&
                hats.GetUnlockedPets().Any(i=>i.ProductId==x.Pet) &&
                hats.GetUnlockedNamePlates().Any(i=>i.ProductId==x.NamePlate);
        }
        public static bool Save(string name)
        {
            try {
                if(!CanEdit()) { Status="Save outfits outside an active game or countdown."; return false; }
                var c=DataManager.Player.Customization;
                var outfit=new WardrobeStore.Outfit{Name=name.Trim(),Hat=c.Hat,Skin=c.Skin,Visor=c.Visor,Pet=c.Pet,NamePlate=c.NamePlate,Color=previewColor>=0?previewColor:PreferredColor>=0?PreferredColor:c.Color};
                if(!Owned(outfit)) { Status="Inventory is not ready or an item is not unlocked. Open the normal wardrobe and retry."; return false; }
                bool ok=WardrobeStore.Add(outfit); Status=WardrobeStore.Status; return ok;
            } catch(Exception) { Status="Could not read your wardrobe. No outfit saved."; return false; }
        }
        public static bool Apply(WardrobeStore.Outfit outfit)
        {
            try {
                if(!CanEdit() || Time.realtimeSinceStartup<nextApply) { Status="Wait five seconds; apply only at the main menu or in a waiting lobby."; return false; }
                // Revalidate inventory on every apply, including presets from disk.
                if(!Owned(outfit)) { Status="An outfit item is no longer unlocked or inventory is loading. Nothing applied."; return false; }
                nextApply=Time.realtimeSinceStartup+5;
                var c=DataManager.Player.Customization;
                var player=AmongUsClient.Instance?.AmConnected==true?PlayerControl.LocalPlayer:null;
                if(player!=null) {
                    if(c.Hat!=outfit.Hat) player.RpcSetHat(outfit.Hat);
                    if(c.Skin!=outfit.Skin) player.RpcSetSkin(outfit.Skin);
                    if(c.Visor!=outfit.Visor) player.RpcSetVisor(outfit.Visor);
                    if(c.Pet!=outfit.Pet) player.RpcSetPet(outfit.Pet);
                    if(c.NamePlate!=outfit.NamePlate) player.RpcSetNamePlate(outfit.NamePlate);
                    RequestColor(player,outfit.Color);
                } else c.Color=(byte)outfit.Color;
                PreferredColor=outfit.Color; previewColor=outfit.Color;
                c.Hat=outfit.Hat; c.Skin=outfit.Skin; c.Visor=outfit.Visor; c.Pet=outfit.Pet; c.NamePlate=outfit.NamePlate;
                Status="Outfit applied. Saved color is preferred; occupied colors fall back left through the palette. Automatic restore checks every five seconds in the lobby."; return true;
            } catch(Exception) { Status="Outfit apply failed; check the normal wardrobe before retrying. Some items may have changed."; return false; }
        }
        private static void RequestColor(PlayerControl player,int preferred)
        {
            var used=new HashSet<int>();
            foreach(var other in PlayerControl.AllPlayerControls)
                if(other!=null && other!=player && other.Data!=null && !other.Data.Disconnected)
                    used.Add(other.Data.DefaultOutfit.ColorId);
            int color=WardrobeColorRules.Select(preferred,Palette.PlayerColors.Length,used);
            nextColor=Time.realtimeSinceStartup+5;
            if(color>=0 && player.Data.DefaultOutfit.ColorId!=color) player.CmdCheckColor((byte)color);
        }
        public static void Tick()
        {
            if(!RestorePreferredColor || PreferredColor<0 || Time.realtimeSinceStartup<nextColor ||
                AmongUsClient.Instance?.AmConnected!=true || !CanEdit()) return;
            RequestColor(PlayerControl.LocalPlayer,PreferredColor);
        }
    }
}
