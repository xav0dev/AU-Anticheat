using AmongUs.GameOptions;
using InnerNet;
using System.Collections.Generic;
using static AUAnticheat.network.Constants;

namespace AUAnticheat
{
	internal static class Utilities
	{
		private static readonly HashSet<SystemTypes> SkeldSabotages = new HashSet<SystemTypes>
		{
			SystemTypes.Reactor, SystemTypes.LifeSupp, SystemTypes.Electrical, SystemTypes.Comms
		};

		private static readonly HashSet<SystemTypes> PolusSabotages = new HashSet<SystemTypes>
		{
			SystemTypes.Laboratory, SystemTypes.Electrical, SystemTypes.Comms
		};

		private static readonly HashSet<SystemTypes> AirshipSabotages = new HashSet<SystemTypes>
		{
			SystemTypes.HeliSabotage, SystemTypes.Electrical, SystemTypes.Comms
		};

		private static readonly HashSet<SystemTypes> FungleSabotages = new HashSet<SystemTypes>
		{
			SystemTypes.Reactor, SystemTypes.Comms, SystemTypes.MushroomMixupSabotage
		};

		public static MapNames GetCurrentMap()
		{
			if(ShipStatus.Instance == null)
			{
				if(AmongUsClient.Instance.NetworkMode == NetworkModes.FreePlay)
				{
					return (MapNames)AmongUsClient.Instance.TutorialMapId;
				}

				return (MapNames)GameOptionsManager.Instance.CurrentGameOptions.MapId;
			}

			return (SpawnType)ShipStatus.Instance.SpawnId switch
			{
				SpawnType.SkeldShipStatus => MapNames.Skeld,
				SpawnType.DleksShipStatus => MapNames.Dleks,
				SpawnType.MiraShipStatus => MapNames.MiraHQ,
				SpawnType.PolusShipStatus => MapNames.Polus,
				SpawnType.AirshipShipStatus => MapNames.Airship,
				SpawnType.FungleShipStatus => MapNames.Fungle,
				_ => MapNames.Skeld
			};
		}

		public static bool IsAnticheatPresent()
		{
			if(Constants.IsVersionModded() || PlayerControl.LocalPlayer == null || PlayerControl.LocalPlayer.Data == null) return false;
			return PlayerControl.LocalPlayer.Data.OwnerId != (int)OwnerIds.Host;
		}

		public static bool IsValidSabotage(SystemTypes system)
		{
			return GetCurrentMap() switch
			{
				MapNames.Skeld or MapNames.Dleks or MapNames.MiraHQ => SkeldSabotages.Contains(system),
				MapNames.Polus => PolusSabotages.Contains(system),
				MapNames.Airship => AirshipSabotages.Contains(system),
				MapNames.Fungle => FungleSabotages.Contains(system),
				_ => false
			};
		}
	}
}
