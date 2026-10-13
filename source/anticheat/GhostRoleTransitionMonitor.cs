using HarmonyLib;

namespace AUAnticheat.anticheat
{
	internal static class GhostRoleTransitionMonitor
	{
		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CoSetRole))]
		private static class OnGhostRoleAssigned
		{
			private static void Prefix(PlayerControl __instance, AmongUs.GameOptions.RoleTypes __0)
			{
				// The role RPC can precede IsDead replication. Use the requested role,
				// not the old role snapshot, and include new game-defined ghost roles.
				if(!RoleManager.IsGhostRole(__0)) return;
				Anticheat.GrantGhostRoleGrace(__instance);
				Anticheat.GrantMovementGrace(__instance, 4);
			}
		}

		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Die))]
		private static class OnPlayerDie
		{
			private static void Prefix(PlayerControl __instance, bool assignGhostRole)
			{
				Anticheat.GrantMovementGrace(__instance, 4);
				if(assignGhostRole) Anticheat.GrantGhostRoleGrace(__instance);
			}
		}

		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Exiled))]
		private static class OnPlayerExiled
		{
			private static void Prefix(PlayerControl __instance)
			{
				Anticheat.GrantMovementGrace(__instance, 4);
				Anticheat.GrantGhostRoleGrace(__instance);
			}
		}
	}
}
