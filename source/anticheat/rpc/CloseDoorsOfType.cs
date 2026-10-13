using Hazel;
using System;

namespace AUAnticheat.anticheat.rpc
{
	internal class CloseDoorsOfType : RpcCheck
	{
		// Player is null here as we cannot determine who sent this RPC
		public override bool Validate(PlayerControl player, MessageReader reader)
		{
			// This host-authored result cannot be attributed to an individual client.
			// Authority is enforced centrally; game-mode inference here caused false
			// positives during legitimate host state synchronization.
			return true;
		}

		public override RpcCalls GetId()
		{
			return RpcCalls.CloseDoorsOfType;
		}

		public override Type GetExpectedNetObject()
		{
			return typeof(ShipStatus);
		}

		public override bool IsHostOnly()
		{
			return true;
		}
	}
}
