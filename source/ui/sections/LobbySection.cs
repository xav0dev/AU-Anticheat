using AUAnticheat.anticheat;
using UnityEngine;
namespace AUAnticheat.ui.sections
{
 internal sealed class LobbySection : ISection
 {
  private bool rules, level, exceptions, loadouts, moderation, automation;
  private readonly ModeratorSection entry = new();
  private readonly ExceptionsSection friends = new();
  public LobbySection() : base("Lobby") { }
  public override void Render()
  {
   Styles.Help("Configure these at any time. Automatic moderation and public messages require host authority.");
   LobbyChat.ShareAlerts=Styles.Toggle(LobbyChat.ShareAlerts,"Share evidence-based detections in chat");
   Styles.Help("Shares threshold or exploit-pattern actions from referenced-owner mode, labeled unverified. No entry-level or unrelated unknown-sender chat notices. Owner-based punishment can affect a spoofed victim.");
   RoundReports.Enabled=Styles.Toggle(RoundReports.Enabled,"Post results after each match");
   LobbyExtension.Enabled=Styles.Toggle(LobbyExtension.Enabled,"Accept offered lobby extensions");
   Styles.Help("Results use ONE multiline message after 5 seconds of lobby readiness: winner, impostor kills, tasks and time. Long rosters use compact text; full roles stay in Alerts. All automatic messages wait for your own chat cooldown too.");
   Styles.Help("Result key: I = Impostor, S = Shapeshifter, P = Phantom, V = Viper; k = observed kills. A ~ marks a shortened name. Full details are saved in Alerts.");
   Styles.Help("Extensions work only when offered and confirmed by the server; this does NOT disable the inactivity countdown.");
   Styles.Help(LobbyExtension.Status);
   automation=Styles.Foldout(automation,"Join protection and automation");
   if(automation)
   {
    HostLobbyAutomation.JoinFloodProtection=Styles.Toggle(HostLobbyAutomation.JoinFloodProtection,"Watch for join floods");
    HostLobbyAutomation.AutoLock=Styles.Toggle(HostLobbyAutomation.AutoLock,"Make lobby private during a join flood");
    GUILayout.Label($"Flood threshold: {HostLobbyAutomation.JoinLimit} joins / {HostLobbyAutomation.JoinWindowSeconds}s");
    HostLobbyAutomation.JoinLimit=(int)GUILayout.HorizontalSlider(HostLobbyAutomation.JoinLimit,4,30);
    HostLobbyAutomation.JoinWindowSeconds=(int)GUILayout.HorizontalSlider(HostLobbyAutomation.JoinWindowSeconds,5,60);
    Styles.Help("Pauses auto-start for 30 seconds. Making the lobby private removes public listing, but code/invite joins may still work. Does not ban new players or reopen the lobby automatically.");
    if(GUILayout.Button("Make this lobby private now")) HostLobbyAutomation.LockNow();
    HostLobbyAutomation.AutoStart=Styles.Toggle(HostLobbyAutomation.AutoStart,"Auto-start (default off)");
    GUILayout.Label("Start target: "+(HostLobbyAutomation.StartPlayers==0 ? "Full lobby" : HostLobbyAutomation.StartPlayers.ToString()));
    if(GUILayout.Button("Use full lobby target")) HostLobbyAutomation.StartPlayers=0;
    if(GUILayout.Button("Use custom player count")) HostLobbyAutomation.StartPlayers=4;
    if(HostLobbyAutomation.StartPlayers>0) HostLobbyAutomation.StartPlayers=(int)GUILayout.HorizontalSlider(HostLobbyAutomation.StartPlayers,4,15);
    GUILayout.Label($"Stable roster delay: {HostLobbyAutomation.StartDelaySeconds}s");
    HostLobbyAutomation.StartDelaySeconds=(int)GUILayout.HorizontalSlider(HostLobbyAutomation.StartDelaySeconds,5,120);
    Styles.Help("Uses normal Start and Play Again actions. Never bypasses game requirements or reconnects after a kick/ban.");
    Styles.Help(HostLobbyAutomation.Status);
   }
   moderation=Styles.Foldout(moderation,"Local moderation and chat filters");
   if(moderation) ModerationTools.Render();
   loadouts=Styles.Foldout(loadouts,"Saved lobby settings");
   if(loadouts) LoadoutsEditor.Render(); else LoadoutsEditor.Editing=false;
   level=Styles.Foldout(level,"Lobby entry level rule");
   if(level)
   {
    entry.RenderLevelSelector();
    exceptions=Styles.Foldout(exceptions,"Friend-code exceptions");
    if(exceptions) friends.Render(); else ExceptionsSection.Editing=false;
   } else ExceptionsSection.Editing=false;
   rules=Styles.Foldout(rules,"Auto-post lobby rules");
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
