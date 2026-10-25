using AUAnticheat.anticheat;
using UnityEngine;
namespace AUAnticheat.ui.sections
{
 internal class GeneralSection : ISection
 {

  private bool gettingStarted;

  public GeneralSection() : base("Overview") { }
  public override void Render()
  {
   GUILayout.Label(StatusIndicator.StateLabel, Styles.Heading);
   Styles.Help(AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost ? "You're the host. Player moderation is available." : "Observer mode. Local protection and alerts; no player removals.");
   Anticheat.Enabled = Styles.Toggle(Anticheat.Enabled, "Cheat detection");
   Styles.Help("ACTIVE means protection hooks loaded and detection is on, not that every check is enabled. PAUSED means detection is off; separate guards may still run. INACTIVE means protections failed to attach.");
   gettingStarted = Styles.Foldout(gettingStarted, "Start here - menu guide");
   if(gettingStarted)
   {
    Styles.Help("Players: select a name to view level, platform and account details. Hosts can confirm a normal kick or lobby ban. Colors match the player's outfit.");
    Styles.Help("Self: saved outfits, your chat filters and automatic lobby return. Lobby: host moderation, entry rules, rules messages and saved settings. The indented Settings opens extended modded-lobby controls.");
    Styles.Help("Protection blocks supported invalid actions. Detection decides what to flag and how the host responds. Alerts keeps the current lobby's notification history.");
    Styles.Help("Settings changes size and transparency. Scroll inside this panel for more options. Insert opens or closes the menu; Escape closes it.");
   }
  }
 }
}
