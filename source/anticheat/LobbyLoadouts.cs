using System;
using UnityEngine;

namespace AUAnticheat.anticheat
{
    internal static class LobbyLoadouts
    {
        private static LoadoutStore store;
        private static float nextApply;
        public static string Status { get; private set; } = "Not initialized.";
        public static string[] Names => store?.Names ?? Array.Empty<string>();
        public static void Initialize(string directory) { store=new LoadoutStore(directory); Status=store.Status; }
        private static bool Ready()
        {
            var client=AmongUsClient.Instance;
            return client != null && client.AmHost && client.ClientId == client.HostId && LobbyBehaviour.Instance != null &&
                MeetingHud.Instance == null && client.GameState == InnerNet.InnerNetClient.GameStates.Joined &&
                GameManager.Instance != null && !GameManager.Instance.GameHasStarted && GameManager.Instance.LogicOptions != null &&
                GameOptionsManager.Instance?.CurrentGameOptions != null && PlayerControl.LocalPlayer != null &&
                (GameStartManager.Instance == null || GameStartManager.Instance.startState == GameStartManager.StartingStates.NotStarting);
        }
        public static bool Save(string name)
        {
            if(!Ready()) { Status="Host a waiting lobby first; stop the start countdown before saving."; return false; }
            try
            {
                byte[] bytes=GameManager.Instance.LogicOptions.gameOptionsFactory.ToBytes(GameOptionsManager.Instance.CurrentGameOptions,false);
                bool ok=store != null && store.Save(name,Application.version,bytes); Status=store?.Status ?? "Storage unavailable."; return ok;
            }
            catch(Exception e) { Status="Could not read the game's settings: "+e.GetType().Name; return false; }
        }
        public static void Delete(string name) { store?.Delete(name); Status=store?.Status ?? "Storage unavailable."; }
        public static bool Apply(string name)
        {
            if(!Ready()) { Status="Only the host can apply a loadout in a waiting lobby, before the start countdown."; return false; }
            if(Time.realtimeSinceStartup < nextApply) { Status="Wait a few seconds before applying another loadout."; return false; }
            var item=store?.Find(name);
            if(item == null || item.Build != Application.version) { Status="Loadout missing or saved on a different game build. Save a new loadout on this build."; return false; }
            var manager=GameOptionsManager.Instance; var logic=GameManager.Instance.LogicOptions;
            var previous=manager.CurrentGameOptions; var previousHost=manager.GameHostOptions;
            bool changed=false;
            try
            {
                var options=logic.gameOptionsFactory.FromBytes(Convert.FromBase64String(item.Payload));
                // Lobby mode/map/capacity are server session properties, not safe
                // to replace silently. Never evict players to fit a saved preset.
                if(options == null || options.Version != previous.Version || options.GameMode != previous.GameMode ||
                   options.MapId != previous.MapId || options.MaxPlayers != previous.MaxPlayers || (options.AreInvalid(options.MaxPlayers) && !CustomLobbySettings.ValidSerialized(options)))
                { Status="Select the same game mode, map and lobby size first. Invalid or incompatible settings were not applied."; return false; }
                nextApply=Time.realtimeSinceStartup+5;
                changed=true; manager.GameHostOptions=options; manager.CurrentGameOptions=options;
                logic.SetGameOptions(options); logic.SyncOptions();
                LobbyRules.Reset();
                Status="Loadout applied through normal game settings. Check the lobby settings before starting."; return true;
            }
            catch(Exception e)
            {
                if(changed) try { manager.GameHostOptions=previousHost; manager.CurrentGameOptions=previous; logic.SetGameOptions(previous); logic.SyncOptions(); } catch(Exception) { }
                Status="Apply failed ("+e.GetType().Name+"). Check settings before starting; a partial network update may have occurred."; return false;
            }
        }
    }
}
