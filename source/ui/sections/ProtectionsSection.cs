using AUAnticheat.features;
using UnityEngine;
namespace AUAnticheat.ui.sections
{
 internal class ProtectionsSection : ISection
 {
  private bool advanced;
  public ProtectionsSection() : base("Protection") { }
  public override void Render()
  {
   Styles.Help("Protect this client from covered removal, movement and message exploits.");
   Protections.ProtectAgainstNonHostKickExploit = Styles.Toggle(Protections.ProtectAgainstNonHostKickExploit, "ErrorKick / ventilation guard");
   Styles.Help("Needs Detection + UpdateSystem checks. Normal vents and host moderation remain available.");
   anticheat.MeetingProtection.Enabled = Styles.Toggle(anticheat.MeetingProtection.Enabled, "Check meeting results");
   anticheat.MeetingProtection.SpamChecks = Styles.Toggle(anticheat.MeetingProtection.SpamChecks,"Filter repeated report requests");
   Styles.Help("Suppresses more than 8 reports in 5 seconds per referenced object. Duplicate/racing reports and early buttons never cause an automatic ban from this check.");
   Styles.Help("Checks supported voting-result messages for invalid data. Normal reports, emergency meetings and role effects must still work.");
   Protections.FilterVoteKickReplays = Styles.Toggle(Protections.FilterVoteKickReplays, "Filter invalid vote-kick requests");
   Styles.Help("Filters covered replay/invalid requests, not legitimate player votes or normal host bans.");
   Protections.BlockUnexpectedLocalTeleport.Enabled = Styles.Toggle(Protections.BlockUnexpectedLocalTeleport.Enabled, "Guard against unwanted teleports");
   Styles.Help("Checks unexpected movement of your player with exceptions for normal transitions. Cannot instantly restore every other player's position.");
   anticheat.InfluencerProtection.Enabled = Styles.Toggle(anticheat.InfluencerProtection.Enabled, "Check Influencer image messages");
   Styles.Help("Validates supported Influencer image data before processing it. Does not disable the legitimate role.");
   Styles.Help("Other players' clients and genuine server disconnects are outside this plugin's control.");
   advanced = Styles.Foldout(advanced, "Advanced safeguards");
   if(!advanced) return;
   Protections.ForceDtls.Enabled = Styles.Toggle(Protections.ForceDtls.Enabled, "Encrypted connection");
   Styles.Help("Requests DTLS for your connection. Encryption is not IP masking or a DDoS shield, and does not change other players' connections.");
   anticheat.LobbyMovementGuard.Enabled = Styles.Toggle(anticheat.LobbyMovementGuard.Enabled, "Lobby wall check (experimental)");
   Styles.Help("Checks explicit lobby teleports before local playback and watches sustained positions beyond the scene's outer bounds. Does not ban for coordinates or treat walking around furniture as crossing a wall. Small wall-edge exploits can remain undetected.");
   Protections.PreflightPackets = Styles.Toggle(Protections.PreflightPackets, "Validate packets before use");
   Protections.BlockLargeGameMessages = Styles.Toggle(Protections.BlockLargeGameMessages, "Block oversized messages");
   Protections.BlockInvalidGameDataMessages = Styles.Toggle(Protections.BlockInvalidGameDataMessages, "Check message types");
   Protections.HardenedReadPackedUInt.Enabled = Styles.Toggle(Protections.HardenedReadPackedUInt.Enabled, "Bound number decoding");
   Protections.MemoryAllocationOverload.Enabled = Styles.Toggle(Protections.MemoryAllocationOverload.Enabled, "Guard voting-result memory");
   Styles.Help("These guards check incoming message structure, size and numbers to limit malformed-data crashes. They do not prevent network-level denial-of-service attacks.");
   Styles.Help($"Blocked packets: {anticheat.PacketPreflight.BlockedPackets}\nLatest: {anticheat.PacketPreflight.LastBlockReason}");
  }
 }
}
