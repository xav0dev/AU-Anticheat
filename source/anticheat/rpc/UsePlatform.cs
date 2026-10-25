using Hazel;

namespace AUAnticheat.anticheat.rpc
{
	internal class UsePlatform : RpcCheck
	{
		public override bool Validate(PlayerControl player, MessageReader reader)
		{
			if(Utilities.IsAnticheatPresent())
			{
				Anticheat.GrantMovementGrace(player, 5);
				return true;
			}
			MapNames map = Utilities.GetCurrentMap();
			if(map != MapNames.Airship)
			{
				Anticheat.Flag(player, $"{player.Data.PlayerName} tried to use a platform outside of the proper map.");
				return false;
			}

			if(ShipStatus.Instance == null)
			{
				Anticheat.Flag(player, $"{player.Data.PlayerName} tried to use a platform when there is no instance of ShipStatus.");
				return false;
			}

			Anticheat.GrantMovementGrace(player, 5);
			return true;
		}

		public override RpcCalls GetId()
		{
			return RpcCalls.UsePlatform;
		}
	}
}
