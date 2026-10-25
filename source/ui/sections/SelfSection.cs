using AUAnticheat.anticheat;
namespace AUAnticheat.ui.sections
{
 internal sealed class SelfSection : ISection
 {
  public SelfSection():base("Self") { }
  public override void Render()
  {
   Styles.Help("Preferences for your own player and the chat you see.");
   WardrobeEditor.Render();
   Wardrobe.RestorePreferredColor=Styles.Toggle(Wardrobe.RestorePreferredColor,"Restore saved outfit color when available");
   Styles.Help("Searches left through the palette if occupied, wrapping at the edge. Retries every five seconds in waiting lobbies, never during rounds or countdowns. Selecting another wardrobe color cancels the previous preference.");
   HostLobbyAutomation.AutoReturn=Styles.Toggle(HostLobbyAutomation.AutoReturn,"Return to lobby after results");
   Styles.Help("Uses Play Again after eight seconds. Does not rejoin after a kick or ban.");
   ModerationTools.RenderPersonal();
  }
 }
}
