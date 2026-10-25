using AUAnticheat.anticheat;
using System;
using UnityEngine;
namespace AUAnticheat.ui.sections
{
 internal class AnticheatSection : ISection
 {
  private bool checks;
  public AnticheatSection() : base("Detection") { }
  public override void Render()
  {
   Styles.Help("Checks game actions this client receives, not software installed on another player's device. An alert is not always proof of cheating.");
   Anticheat.Enabled = Styles.Toggle(Anticheat.Enabled, "Cheat detection");
   Anticheat.SendNotification = Styles.Toggle(Anticheat.SendNotification, "Show detection alerts");
   Anticheat.DiscardInvalidMessages = Styles.Toggle(Anticheat.DiscardInvalidMessages, "Block invalid actions");
   Styles.Help("Stops covered invalid messages before this client applies them. Turning this off can allow a flagged action to continue locally.");
   Anticheat.CheckSpoofedPlatforms = Styles.Toggle(Anticheat.CheckSpoofedPlatforms, "Check platform inconsistencies");
   Anticheat.EnhancedMenuBehaviorChecks = Styles.Toggle(Anticheat.EnhancedMenuBehaviorChecks, "Extra behaviour checks");
   Styles.Help("Also groups sustained mixed-action patterns in Alerts > Related suspicious actions. This helps review events, but cannot verify a forged sender or add ban points.");
   Styles.Help("Looks for inconsistent game behaviour, not menu names. Reported platforms can be spoofed; they are not device scans or verified identities.");
   Styles.Help("Scan-animation replays and action floods can be filtered locally. A fake walk animation or hotkey used only on another device cannot be reliably detected here.");
   Styles.HeadingText("Host response");
   Anticheat.UseReferencedOwnerPunishment=Styles.Toggle(Anticheat.UseReferencedOwnerPunishment,"Use referenced-owner punishment (spoofable)");
   Styles.Help("HostGuard-style attribution: qualifying PlayerControl RPC detections count against the referenced object's owner while you host. This does NOT verify the sender; forged messages can punish an innocent player. Turn this off for observation-only attribution. Shared-object, chat/identity, movement and platform observations stay excluded.");
   GUILayout.Label("Automatic action: " + Anticheat.Punishment);
   GUILayout.BeginHorizontal();
   try { foreach(var action in new[]{Anticheat.Punishments.None,Anticheat.Punishments.Kick,Anticheat.Punishments.Ban,Anticheat.Punishments.PersistentBan})
    if(GUILayout.Button((Anticheat.Punishment==action ? "> " : "")+(action==Anticheat.Punishments.None?"Alert":action==Anticheat.Punishments.PersistentBan?"Persistent Ban":action.ToString()))) Anticheat.Punishment=action; }
   finally { GUILayout.EndHorizontal(); }
   Styles.Help("Alert records without removal. Kick removes from this lobby. Ban prevents ordinary rejoining. Persistent Ban (default) also saves the matching game-reported friend code locally. These choices apply to threshold and immediate exploit-pattern responses. Only the host can remove players.");
   Styles.Help("Persistent bans are local, not verified global identities. Spoofable owner attribution can punish an innocent player. Review and undo mistakes in Lobby > Local moderation.");
   GUILayout.Label($"Point threshold: {Anticheat.EnforcementThreshold}");
   Anticheat.EnforcementThreshold=(int)Math.Round(GUILayout.HorizontalSlider(Anticheat.EnforcementThreshold,50,200));
   Styles.Help("Minor: 50 points. Major: 100. Referenced-owner mode can apply these points without a verified sender. Other unverified observations remain alerts only.");
   Styles.Help("A lower threshold acts sooner but leaves less room for mistakes. Review Alerts before changing this. Owner association is not proof of who sent an action.");
   checks=Styles.Foldout(checks,"Individual message checks");
   if(checks)
   {
    Styles.Help("Advanced troubleshooting: these are the game's message names. Switching one off disables its check, not the player's ability to use that normal action.");
    foreach(var entry in Anticheat.RpcHandlers)
     entry.Value.Enabled=Styles.Toggle(entry.Value.Enabled,entry.Key.ToString());
   }
  }
 }
}
