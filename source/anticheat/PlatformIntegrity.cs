using HarmonyLib;
using InnerNet;

namespace AUAnticheat.anticheat
{
	// Detection only. No device identifiers are logged, persisted, or displayed.
	internal static class PlatformIntegrity
	{
		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Start))]
		private static class OnPlayerStart
		{
			private static void Postfix(PlayerControl __instance)
			{
				if(!Anticheat.Enabled || !Anticheat.CheckSpoofedPlatforms) return;

				ClientData client = AmongUsClient.Instance.GetClientFromCharacter(__instance);
				if(client == null || IsConsistent(client.PlatformData)) return;

				Anticheat.Flag(__instance, $"{client.PlayerName} sent inconsistent platform information ({client.PlatformData.Platform}).", false);
			}
		}

		public static bool IsConsistent(PlatformSpecificData platform)
		{
			string name = platform.PlatformName;
			if(string.IsNullOrEmpty(name)) return false;
			bool hasXboxId = platform.XboxPlatformId != 0;
			bool hasPlayStationId = platform.PsnPlatformId != 0;

			switch(platform.Platform)
			{
				case Platforms.StandaloneEpicPC:
				case Platforms.StandaloneSteamPC:
				case Platforms.StandaloneMac:
				case Platforms.StandaloneItch:
				case Platforms.IPhone:
				case Platforms.Android:
				case (Platforms)112:
					return IsGenericName(name) && !hasXboxId && !hasPlayStationId;

				case Platforms.StandaloneWin10:
					return IsGenericName(name) && hasXboxId && !hasPlayStationId;

				case Platforms.Xbox:
					return !IsGenericName(name) && name.Length >= 3 && name.Length <= 16 && hasXboxId && !hasPlayStationId;

				case Platforms.Playstation:
					return !IsGenericName(name) && !hasXboxId && hasPlayStationId;

				case Platforms.Switch:
					return !IsGenericName(name) && !hasXboxId && !hasPlayStationId;

				case (Platforms)255:
					return AmongUsClient.Instance.NetworkMode == NetworkModes.LocalGame;

				default:
					return false;
			}
		}

		private static bool IsGenericName(string name)
		{
			return name == "TESTNAME";
		}
	}
}
