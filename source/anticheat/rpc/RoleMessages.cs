using System;
using Hazel;

namespace AUAnticheat.anticheat.rpc
{
	// Shared objects expose no authenticated sender here. Structural rejection
	// must never ban a claimed player/target.
	internal class SpiritGuideMessage : RpcCheck
	{
		public override RpcCalls GetId() => RpcCalls.SpiritGuideMessage;
		public override Type GetExpectedNetObject() => typeof(ShipStatus);
		public override bool Validate(PlayerControl player, MessageReader reader)
			=> PacketPreflight.ValidateRpc(GetId(), reader);
	}

	internal class QueueOverruleVotes : RpcCheck
	{
		public override RpcCalls GetId() => RpcCalls.QueueOverruleVotes;
		public override Type GetExpectedNetObject() => typeof(MeetingHud);
		public override bool Validate(PlayerControl player, MessageReader reader)
			=> PacketPreflight.ValidateRpc(GetId(), reader);
	}
}
