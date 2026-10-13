using UnityEngine;
using AUAnticheat.anticheat;
namespace AUAnticheat.ui
{
 internal static class RulesEditor
 {
  public static bool Editing;
  internal static string Draft="";
  public static void Type(string input)
  {
   if(!Editing || input==null) return;
   foreach(char c in input)
   {
    if(c=='\b') { if(Draft.Length>0) Draft=Draft.Substring(0,Draft.Length-1); }
    else if(c=='\u001b') Editing=false;
    else if(c=='\r') { if(Draft.Length<4096) Draft+='\n'; }
    else if((c=='\n'||!char.IsControl(c))&&Draft.Length<4096) Draft+=c;
   }
  }
  public static void Render()
  {
   Styles.Help(LobbyRules.MessageInfo);
   if(!Editing)
   {
    if(GUILayout.Button("Edit rules")) { Draft=LobbyRules.Preview; Editing=true; LoadoutsEditor.Editing=sections.ExceptionsSection.Editing=false; }
    if(GUILayout.Button("Reload saved rules")) LobbyRules.Reload();
    GUILayout.Label(LobbyRules.Preview);
    return;
   }
   Styles.Help("Type here. Enter adds a rule; Backspace deletes; Escape stops editing without saving. Close game chat first. All rules must fit one 100-character message including separators. No label is added. Overlong edits cannot replace your saved rules.");
   Styles.Help(LobbyRules.DraftMessageInfo(Draft));
   GUILayout.Label(Draft+" |",Styles.SectionBox);
   if(GUILayout.Button("Clear draft")) Draft="";
   if(GUILayout.Button("Save rules")&&LobbyRules.SaveText(Draft)) Editing=false;
   if(GUILayout.Button("Cancel editing")) Editing=false;
  }
 }
}
