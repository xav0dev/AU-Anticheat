namespace AUAnticheat.anticheat
{
	internal static class LobbyAdmissionRules
	{
		public static bool ShouldBan(bool enabled, bool host, bool waiting, bool hostPlayer,
			bool dataReady, bool exception, uint reportedLevel, uint minimum)
			=> enabled && host && waiting && !hostPlayer && dataReady && !exception &&
				reportedLevel != uint.MaxValue && minimum >= 1 && (ulong)reportedLevel + 1 < minimum;
	}
}
