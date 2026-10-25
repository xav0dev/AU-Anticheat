using Hazel;
using System;

namespace AUAnticheat.anticheat.rpc
{
	internal class ExitVent : RpcCheck
	{
		// Sending ExitVent RPCs can be used to make the player teleport to areas without having to send SnapTo RPCs
		public override bool Validate(PlayerControl player, MessageReader reader)
		{
			int ventId = reader.ReadPackedInt32();
			if(!VentRuleHelpers.ValidateLoadedVent(ventId)) return false;
			// Engineer timeout, ejection, death and role changes can precede exit
			// locally. !inVent/current CanVent cannot prove a forged cleanup packet.
			Anticheat.GrantMovementGrace(player);
			return true;
		}

		public override RpcCalls GetId()
		{
			return RpcCalls.ExitVent;
		}

		public override Type GetExpectedNetObject()
		{
			return typeof(PlayerPhysics);
		}
	}
}
