using System;
using UnityEngine;

namespace AUAnticheat.ui
{
    // Input.inputString avoids stripped IL2CPP GUI.TextField methods.
    internal static class InlineEditor
    {
        public static bool Editing => apply != null;
        internal static string Draft = "", Error = "", Key = "";
        private static Func<string,bool> apply;
        private static int limit;
        public static void Cancel() { apply=null; Key=""; Error=""; }
        public static void Begin(string key,string value,int max,Func<string,bool> save)
        {
            Key=key; Draft=value??""; limit=max; apply=save; Error="";
            RulesEditor.Editing=LoadoutsEditor.Editing=sections.ExceptionsSection.Editing=false;
        }
        public static void Commit()
        {
            if(apply==null) return;
            try { if(apply(Draft)) Cancel(); else Error="Not saved. Check the value and the message below."; }
            catch(Exception) { Error="Could not save. Your previous value is unchanged."; }
        }
        public static void Type(string text)
        {
            if(!Editing || text==null) return;
            foreach(char c in text)
            {
                if(c=='\u001b') { Cancel(); break; }
                if(c=='\r'||c=='\n') { Commit(); break; }
                if(c=='\b') { if(Draft.Length>0) Draft=Draft[..^1]; }
                else if(!char.IsControl(c) && Draft.Length<limit) Draft+=c;
            }
        }
        public static void Field(string key,string value,int max,Func<string,bool> save)
        {
            if(Key!=key || !Editing)
            {
                if(GUILayout.Button(key+": "+value+"  [Edit]")) Begin(key,value,max,save);
                return;
            }
            GUILayout.Label(key+": "+Draft+" |",Styles.SectionBox);
            Styles.Help("Type a value. Enter saves; Escape cancels. Close game chat while editing.");
            GUILayout.BeginHorizontal();
            try {
                if(GUILayout.Button("Clear")) Draft="";
                if(GUILayout.Button("Save value")) Commit();
                if(GUILayout.Button("Cancel")) Cancel();
            } finally { GUILayout.EndHorizontal(); }
            if(Error.Length>0) Styles.Help(Error);
        }
    }
}
