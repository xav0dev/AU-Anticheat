using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace AUAnticheat.anticheat
{
	internal static class MeetingProtection
	{
		public static bool Enabled = true;
		[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.VotingComplete))]
		private static class BeforeResults
		{
			private static bool Prefix(Il2CppStructArray<MeetingHud.VoterState> __0)
			{
				if(!Enabled) return true;
				if(__0 == null || __0.Length > 252) return PacketPreflight.Block("invalid meeting result size");
				byte[] voters = new byte[__0.Length];
				for(int i = 0; i < voters.Length; i++) voters[i] = __0[i].VoterId;
				return MeetingResultRules.UniqueVoters(voters) || PacketPreflight.Block("meeting result lists an invalid or repeated voter");
			}
		}
		[HarmonyPatch(typeof(GameData), nameof(GameData.OnMeetingStart))]
		private static class MeetingStarted
		{
			private static void Prefix()
			{
				Anticheat.GrantMeetingTransitionGrace(3);
				foreach(var player in PlayerControl.AllPlayerControls) Anticheat.GrantMovementGrace(player, 4);
			}
		}
	}
}
