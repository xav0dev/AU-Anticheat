namespace AUAnticheat.anticheat
{
	internal static class MovementSafetyRules
	{
		public static bool CanInspectLivingMovement(bool activeRound, bool initialized,
			bool deadOrGhost, bool transition, bool meeting, bool traversal)
			=> activeRound && initialized && !deadOrGhost && !transition && !meeting && !traversal;
	}
}
