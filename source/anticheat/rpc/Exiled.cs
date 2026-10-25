using Hazel;

namespace AUAnticheat.anticheat.rpc
{
	internal class Exiled : RpcCheck
	{
		public override bool Validate(PlayerControl player, MessageReader reader)
		{
			// Some versions send a host-authored Exiled result while others resolve
			// ejection locally. Both are legitimate death-to-ghost transitions.
			Anticheat.GrantGhostRoleGrace(player);
			return true;
		}

		public override RpcCalls GetId()
		{
			return RpcCalls.Exiled;
		}

		public override bool IsHostOnly()
		{
			return true;
		}
	}
}
