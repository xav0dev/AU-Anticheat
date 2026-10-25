using System;
using UnityEngine;
namespace AUAnticheat.ui.sections
{
 internal class MenuSection : ISection
 {
  private bool confirmEject;
  private bool confirmReset;
  private bool chooseKey;
  public MenuSection() : base("Settings") { }
  public override void Render()
  {
   Styles.Help(anticheat.SavedSettings.Status);
   Styles.HeadingText("Menu key");
   Styles.Help("Current key: " + MainUI.MenuKey + ". Pick a function key that does not conflict with other software.");
   chooseKey=Styles.Foldout(chooseKey,"Choose menu key");
   if(chooseKey) foreach(string key in new[]{"Insert","Home","F1","F2","F3","F4","F5","F6","F7","F8","F9","F10","F11","F12"})
    if(GUILayout.Button((MainUI.MenuKey.ToString()==key ? "> " : "")+key)) MainUI.MenuKey=Enum.Parse<KeyCode>(key);
   if(GUILayout.Button("Reset all settings to defaults")) confirmReset=true;
   if(confirmReset)
   {
    Styles.Help("Resets every preference and clears friend-code exceptions. Keeps your rules text and saved alert history. This does not undo bans.");
    if(GUILayout.Button("Confirm reset all settings",Styles.Danger)) { anticheat.SavedSettings.ResetDefaults(); confirmReset=false; }
    if(GUILayout.Button("Cancel reset")) confirmReset=false;
   }
   Styles.HeadingText("Appearance");
   Styles.Help(GameFont.Current != null ? "Using the game's loaded font." : "Using a fallback font: this game build has not exposed a compatible source font.");
   GUILayout.Label($"Menu size: {MainUI.scale:0.00}x");
   MainUI.scale=(float)Math.Round(GUILayout.HorizontalSlider(MainUI.scale,.75f,1.5f),2);
   Styles.Help("Automatically fits your screen. Drag only the top bar to move.");
   GUILayout.Label($"Background opacity: {Styles.menuOpacity*100:0}%");
   Styles.menuOpacity=(float)Math.Round(GUILayout.HorizontalSlider(Styles.menuOpacity,.35f,1f),2);
   Styles.Help("Lower = more see-through. Applies to this menu, the top-right status badge and pop-up alerts. Text and icons stay solid so they remain readable.");
   if(GUILayout.Button("Reset appearance")) { MainUI.scale=1; Styles.menuOpacity=.75f; Styles.primaryColor=Styles.UIColors.Cardinal; MainUI.windowPosition=new Vector2(90,70); }
   Styles.HeadingText("Privacy");
   Styles.Help("Exceptions are saved locally. Diagnostics can appear in BepInEx logs. Shared alerts use your normal chat.");
   if(GUILayout.Button("Clear session evidence")) { anticheat.Anticheat.ResetSession(); anticheat.ActivityTracker.Reset(); }
   Styles.Help("Clears current detection scores and observation history. It does not undo bans or remove saved friend-code exceptions.");
   Styles.HeadingText("Plugin");
   if(GUILayout.Button("Unload plugin",Styles.Danger)) confirmEject=!confirmEject;
   if(confirmEject) { Styles.Help("This stops protections until you restart the game."); if(GUILayout.Button("Confirm unload",Styles.Danger)) AUAnticheatPlugin.Eject(); }
  }
 }
}
