using UnityEngine;
using AUAnticheat.anticheat;
namespace AUAnticheat.ui
{
    internal static class LoadoutsEditor
    {
        public static bool Editing;
        internal static string Draft="";
        private static string pendingName, pendingAction;
        public static void Type(string input)
        {
            if(!Editing || input == null) return;
            foreach(char c in input)
            {
                if(c=='\b') { if(Draft.Length>0) Draft=Draft.Substring(0,Draft.Length-1); }
                else if(c=='\r'||c=='\n'||c=='\u001b') { Editing=false; break; }
                else if(Draft.Length<32 && (char.IsLetterOrDigit(c)||c==' '||c=='-'||c=='_')) Draft+=c;
            }
        }
        public static void Render()
        {
            Styles.Help("Save the host's current game settings, including roles. Apply only in a waiting lobby with the same mode, map and capacity. Up to 20 named loadouts, saved on this device.");
            if(GUILayout.Button(Editing?"Stop naming loadout":"Name new loadout")) { Editing=!Editing; RulesEditor.Editing=sections.ExceptionsSection.Editing=false; }
            GUILayout.Label("Name: "+Draft+(Editing?" |":""));
            if(Editing) Styles.Help("Close game chat before typing. Enter stops typing. Then press Save current lobby settings.");
            if(GUILayout.Button("Save current lobby settings") && LobbyLoadouts.Save(Draft.Trim())) { Editing=false; Draft=""; }
            foreach(string name in LobbyLoadouts.Names)
            {
                GUILayout.Label(name,Styles.Heading);
                GUILayout.BeginHorizontal();
                if(GUILayout.Button("Apply "+name)) { pendingName=name; pendingAction="Apply"; }
                if(GUILayout.Button("Delete "+name)) { pendingName=name; pendingAction="Delete"; }
                GUILayout.EndHorizontal();
            }
            if(pendingName != null)
            {
                Styles.Help(pendingAction+" loadout '"+pendingName+"'?");
                if(GUILayout.Button("Confirm "+pendingAction))
                { if(pendingAction=="Apply") LobbyLoadouts.Apply(pendingName); else LobbyLoadouts.Delete(pendingName); pendingName=null; }
                if(GUILayout.Button("Cancel loadout action")) pendingName=null;
            }
            Styles.Help(LobbyLoadouts.Status);
        }
    }
}
