using AUAnticheat.anticheat;
using UnityEngine;
namespace AUAnticheat.ui.sections
{
 internal sealed class LobbySection : ISection
 {
  private bool rules, level, exceptions, loadouts, moderation;
  private readonly ModeratorSection entry = new();
  private readonly ExceptionsSection friends = new();
  public LobbySection() : base("Lobby") { }
  public override void Render()
  {
   Styles.Help("Configure these at any time. Automatic moderation and public messages require host authority.");
   LobbyChat.ShareAlerts=Styles.Toggle(LobbyChat.ShareAlerts,"Share evidence-based detections in chat");
   Styles.Help("Names a player only after attributable evidence reaches the threshold, or a confirmed exploit. No entry-level or unknown-sender chat notices.");
   RoundReports.Enabled=Styles.Toggle(RoundReports.Enabled,"Post results after each match");
   LobbyExtension.Enabled=Styles.Toggle(LobbyExtension.Enabled,"Accept offered lobby extensions");
   Styles.Help("Results use ONE message after 5 seconds of lobby readiness. Includes winner, duration, impostor roles and observed kills. Overlong results stay in local Alerts. All automatic messages wait for your own chat cooldown too.");
   Styles.Help("Result key: I = Impostor, S = Shapeshifter, P = Phantom, V = Viper; k = observed kills. A ~ marks a shortened name. Full details are saved in Alerts.");
   Styles.Help("Extensions work only when offered and confirmed by the server; this does NOT disable the inactivity countdown.");
   Styles.Help(LobbyExtension.Status);
   moderation=Styles.Foldout(moderation,"Local moderation and chat filters");
   if(moderation) ModerationTools.Render();
   loadouts=Styles.Foldout(loadouts,"Lobby settings loadouts");
   if(loadouts) LoadoutsEditor.Render(); else LoadoutsEditor.Editing=false;
   level=Styles.Foldout(level,"Lobby entry rule");
   if(level) entry.RenderLevelSelector();
   exceptions=Styles.Foldout(exceptions,"Friend-code exceptions");
   if(exceptions) friends.Render(); else ExceptionsSection.Editing=false;
   rules=Styles.Foldout(rules,"Saved lobby rules");
   if(!rules) { RulesEditor.Editing=false; return; }
   LobbyRules.Enabled=Styles.Toggle(LobbyRules.Enabled,"Auto-post rules when lobby is full");
   GUILayout.Label($"Full lobby for: {LobbyRules.DelaySeconds} seconds");
   LobbyRules.DelaySeconds=(int)System.Math.Round(GUILayout.HorizontalSlider(LobbyRules.DelaySeconds,1,120));
   Styles.Help("Host only. ONE rules message each time the lobby fills and the same full roster stays for your chosen delay. A departure or replacement restarts the timer. No repeats while it stays full and unchanged. Your normal chat cooldown still applies. No splitting or truncation.");
   RulesEditor.Render();
   Styles.Help(LobbyRules.Status);
   Styles.Help("Saved file: " + LobbyRules.FilePath);
  }
 }
}
