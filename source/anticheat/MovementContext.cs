namespace AUAnticheat.anticheat
{
	internal static class MovementContext
	{
		public static bool StableLivingPlayer(PlayerControl player)
		{
			if(player == null || player.Data == null) return false;
			return MovementSafetyRules.CanInspectLivingMovement(
				GameManager.Instance != null && GameManager.Instance.GameHasStarted && LobbyBehaviour.Instance == null && ShipStatus.Instance != null,
				player.roleAssigned && player.Data.Role != null && player.MyPhysics != null,
				player.Data.IsDead || RoleManager.IsGhostRole(player.Data.RoleType),
				Anticheat.IsRoundStartGrace() || Anticheat.HasMovementGrace(player) || Anticheat.HasGhostRoleGrace(player) || Anticheat.HasMeetingTransitionGrace(),
				MeetingHud.Instance != null,
				player.inVent || player.onLadder || player.inMovingPlat || (player.MyPhysics != null && player.MyPhysics.DoingCustomAnimation));
		}
	}
}
