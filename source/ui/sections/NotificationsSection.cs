using UnityEngine;
namespace AUAnticheat.ui.sections
{
 internal sealed class NotificationsSection : ISection
 {
  private bool evidence;
  private bool activity;
  private bool sequences;
  public NotificationsSection() : base("Alerts") { }
  public override void Render()
  {
   var notices=AUAnticheatPlugin.notifications;
   GUILayout.Label($"{anticheat.ActivityTracker.Count} players observed   /   {notices.HistoryCount} saved alerts");
   Styles.Help("Unknown senders are never automatically banned. Local protection cannot stop every attack.");
   sequences=Styles.Foldout(sequences,"Related suspicious actions");
   if(sequences)
   {
    Styles.Help("Groups spaced invalid actions involving the same player object. Normal transitions and short bursts are discounted. These are review clues, not proof that the named player cheated. No public accusations are sent.");
    Styles.Help(anticheat.Anticheat.GetSequenceSummary());
   }
   activity=Styles.Foldout(activity,"Activity details");
   if(activity) { Styles.Help(anticheat.ActivityTracker.Summary()); Styles.Help(anticheat.Anticheat.GetRecentIncidentSummary()); }
   notices.DisableNotifications=!Styles.Toggle(!notices.DisableNotifications,"Show pop-up alerts");
   Styles.Help("Pop-ups last 10 seconds. Saved alerts stay until you leave or clear them.");
   if(GUILayout.Button("Clear alert history")) notices.ClearHistory();
   notices.ShowTechnicalDetails=Styles.Toggle(notices.ShowTechnicalDetails,"Technical detail");
   Styles.Help("Leave technical detail off for plain-language alerts. Turn it on when sharing a bug report. A blocked action is not necessarily a ban; an unknown sender cannot be safely identified.");
   GUILayout.Label(notices.GetHistorySummary(200));
   evidence=Styles.Foldout(evidence,"Sender evidence");
   if(!evidence) return;
   Styles.Help("Claimed IDs are not verified senders. No IP or device IDs are collected.");
   if(GUILayout.Button("Clear sender evidence")) anticheat.SenderEvidence.Reset();
   GUILayout.Label(anticheat.SenderEvidence.Summary());
  }
 }
}
