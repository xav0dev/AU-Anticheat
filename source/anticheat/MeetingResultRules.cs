using System;

namespace AUAnticheat.anticheat
{
	internal static class MeetingResultRules
	{
		// Check only immutable shape, not stale role/alive/meeting snapshots.
		// Vote targets retain all native sentinel values (skip/dead/abstain).
		public static bool UniqueVoters(ReadOnlySpan<byte> voters)
		{
			if(voters.Length > 252) return false;
			Span<bool> seen = stackalloc bool[256];
			seen.Clear();
			foreach(byte id in voters)
			{
				if(id >= 253 || seen[id]) return false;
				seen[id] = true;
			}
			return true;
		}
	}
}
