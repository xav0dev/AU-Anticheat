using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace AUAnticheat.anticheat
{
	internal static class MeetingProtection
	{
		public static bool Enabled = true;
		public static bool SpamChecks = true;
		private static readonly System.Collections.Generic.Dictionary<int, (uint Net, EventWindow Window)> Reports = new();
		private static string reportRoom = "";
		private static float nextNotice;
		public static bool AllowReport(PlayerControl player)
		{
			if(!Enabled || !SpamChecks || player?.Data == null) return true;
			string room = $"{AmongUsClient.Instance?.GameId}:{AmongUsClient.Instance?.HostId}:{ShipStatus.Instance?.GetInstanceID()}";
			if(room != reportRoom) { Reports.Clear(); reportRoom = room; nextNotice = 0; }
			if(!Reports.TryGetValue(player.OwnerId, out var entry) || entry.Net != player.NetId)
			{
				if(Reports.Count >= 64) Reports.Clear();
				Reports[player.OwnerId] = entry = (player.NetId, new EventWindow());
			}
			float now = UnityEngine.Time.realtimeSinceStartup;
			if(entry.Window.Add(now, 5) <= 8) return true;
			if(now >= nextNotice)
			{
				nextNotice = now + 10;
				AUAnticheatPlugin.notifications.Send("Meeting protection", $"Ignored repeated report requests referencing {PublicText.Name(player.Data.PlayerName)}. Sender unverified; no automatic punishment.");
			}
			return false;
		}
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
