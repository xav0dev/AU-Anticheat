using System;

namespace AUAnticheat.anticheat
{
	internal static class MovementRecoveryRules
	{
		public const float JoinGraceSeconds = 3;
		public const float MaximumSampleGap = 0.5f;
		public const float WallMargin = 0.2f;
		public static bool Finite(float x, float y) => float.IsFinite(x) && float.IsFinite(y);
		public static bool Fresh(float elapsed) => float.IsFinite(elapsed) && elapsed >= 0 && elapsed <= MaximumSampleGap;
		public static bool Crossed(float distance, float hitDistance)
			=> float.IsFinite(distance) && float.IsFinite(hitDistance) && hitDistance > 0.01f && hitDistance <= distance && distance - hitDistance > WallMargin;
		public static bool CanBroadcast(bool isLocalPlayer, bool isHost, bool serverAuthority)
			// Owning a player does not authorize corrective jumps on official servers.
			=> isHost && !serverAuthority;
		public static bool ConfirmedMotion(int observations, float duration)
			=> observations >= 2 && float.IsFinite(duration) && duration >= 0.12f && duration <= 1;
	}
}
