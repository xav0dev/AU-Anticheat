using System;

namespace AUAnticheat.anticheat
{
	internal static class VentilationRules
	{
		// AFTER system and player fields: ushort sequence, byte operation, byte vent ID.
		public static bool TryRead(ReadOnlySpan<byte> payload, out ushort sequence, out byte operation, out byte vent)
		{
			sequence = 0; operation = 0; vent = 0;
			if(payload.Length < 4) return false;
			sequence = (ushort)(payload[0] | (payload[1] << 8));
			operation = payload[2]; vent = payload[3];
			return operation <= 5;
		}
		public static bool IsMovement(byte operation) => operation >= 2 && operation <= 4;
		public static bool ShouldCheckEntryRole(bool authority, bool initialized, bool transition, bool dead)
			=> authority && initialized && !transition && !dead;
		public static bool RoleAllowsEntry(bool canVent, bool engineer) => canVent || engineer;
	}
}
