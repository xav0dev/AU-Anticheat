using AUAnticheat.anticheat;
using UnityEngine;
namespace AUAnticheat.ui.sections
{
 internal sealed class LobbySettingsSection : ISection
 {
  private bool extended=true,gameplay,tasks;
  public LobbySettingsSection():base("  Settings") { }
  public override void Render()
  {
   extended=Styles.Foldout(extended,"Extended settings (modded lobbies only)");
   if(extended)
   {
    Styles.Help("Gameplay-changing mode. Select before connecting, then host a NEW lobby. Normal public lobbies are not changed. This uses Innersloth's documented +25 host-mod registration; not every custom value is guaranteed to work on every client.");
    if(GUILayout.Button(CustomLobbySettings.Enabled?"Switch to normal mode (offline only)":"Select modded +25 mode (offline only)")) CustomLobbySettings.SetMode(!CustomLobbySettings.Enabled);
    if(GUILayout.Button("Read current settings into draft")) CustomLobbySettings.ReadCurrent();
    if(CustomLobbySettings.Enabled)
    {
     foreach(var item in CustomLobbySettings.Options)
     {
      if(item.Boolean) { item.Value=Styles.Toggle(item.Value==1,item.Label)?1:0; continue; }
      InlineEditor.Field(item.Label,item.Value.ToString("G",System.Globalization.CultureInfo.InvariantCulture),24,text=>CustomLobbySettings.SetDraft(item.Name,text));
      Styles.Help($"Range: {item.Min} to {item.Max}"+(item.Integer?" (whole numbers).":". Decimals accepted; dot or comma."));
     }
     gameplay=Styles.Foldout(gameplay,"Devices, sabotages, meetings and tasks");
     if(gameplay) Styles.Help("Device restrictions use temporary communications disruption near matching consoles for guests, and block console use for you. This can also hide tasks or disable other comms-dependent abilities while nearby. Real comms sabotage takes priority. Not a security barrier against modified clients; Scientist's portable vitals and Fungle binoculars are not covered.");
     if(gameplay) foreach(var item in LobbyGameplayRules.Options)
     {
      if(item.Toggle) item.Value=Styles.Toggle(item.Value==1,item.Label)?1:0;
      else { InlineEditor.Field(item.Label,item.Value.ToString("G",System.Globalization.CultureInfo.InvariantCulture),24,text=>LobbyGameplayRules.SetDraft(item.Key,text)); Styles.Help($"Range: {item.Min} to {item.Max}"); }
     }
     tasks=Styles.Foldout(tasks,"Disable selected task types");
     if(tasks)
     {
      bool noScan=Styles.Toggle(LobbyGameplayRules.DisabledTasks.Contains("SubmitScan"),"Disable MedBay scan (prevent scan clears)");
      if(noScan) LobbyGameplayRules.DisabledTasks.Add("SubmitScan"); else LobbyGameplayRules.DisabledTasks.Remove("SubmitScan");
      Styles.Help("Task-pool filters apply when tasks are assigned. Leave at least one task of each requested length enabled; an empty pool keeps its original tasks. No cheat points are awarded for these lobby rules.");
      foreach(string task in System.Enum.GetNames(typeof(TaskTypes)))
       if(task!="SubmitScan")
      { bool disabled=Styles.Toggle(LobbyGameplayRules.DisabledTasks.Contains(task),"Disable "+task); if(disabled) LobbyGameplayRules.DisabledTasks.Add(task); else LobbyGameplayRules.DisabledTasks.Remove(task); }
     }
     if(GUILayout.Button("Apply extended settings to my modded lobby")) CustomLobbySettings.Apply();
    }
    Styles.Help(CustomLobbySettings.Status);
   Styles.Help(LobbyGameplayRules.Status);
   }
  }
 }
}
