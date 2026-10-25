using Hazel;
using System;

namespace AUAnticheat.anticheat.rpc
{
	internal class ClimbLadder : RpcCheck
	{
		public override bool Validate(PlayerControl player, MessageReader reader)
		{
			byte ladderId = reader.ReadByte();
			reader.ReadByte(); // animation sequence, including wrap-around
			if(ShipStatus.Instance != null && ShipStatus.Instance.Ladders != null && !Anticheat.IsRoundStartGrace())
			{
				bool found = false;
				foreach(Ladder ladder in ShipStatus.Instance.Ladders)
				{
					if(ladder != null && ladder.Id == ladderId) { found = true; break; }
				}
				if(!found)
				{
					Anticheat.Flag("Blocked a ladder action for an identifier absent from the loaded map. Sender unverified.");
					return false;
				}
			}
			if(Utilities.IsAnticheatPresent())
			{
				Anticheat.GrantMovementGrace(player, 4);
				return true;
			}
			if(ShipStatus.Instance == null)
			{
				Anticheat.Flag(player, $"{player.Data.PlayerName} tried to climb a ladder when there is no instance of ShipStatus.");
				return false;
			}

			MapNames map = Utilities.GetCurrentMap();
			if(map != MapNames.Airship && map != MapNames.Fungle)
			{
				Anticheat.Flag(player, $"{player.Data.PlayerName} tried to climb a ladder outside of the proper map.");
				return false;
			}

			// A ladder animation can cross a death-state update on another client. It is
			// harmless once dead, so packet ordering is not treated as evidence.
			if(player.Data.IsDead)
			{
				Anticheat.GrantMovementGrace(player, 2);
				return true;
			}

			Anticheat.GrantMovementGrace(player, 4);
			return true;
		}

		public override RpcCalls GetId()
		{
			return RpcCalls.ClimbLadder;
		}

		public override Type GetExpectedNetObject()
		{
			return typeof(PlayerPhysics);
		}
	}
}
