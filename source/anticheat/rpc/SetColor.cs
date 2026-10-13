using Hazel;

namespace AUAnticheat.anticheat.rpc
{
	internal class SetColor : RpcCheck
	{
		public override bool Validate(PlayerControl player, MessageReader reader)
		{
			uint netId = reader.ReadUInt32();
			byte color = reader.ReadByte();

			// This net id field written in the RPC is seemingly useless as the client RPC handler does not do anything with this value
			if(netId != player.Data.NetId)
			{
				SenderEvidence.NoteObjectClaim(netId, player.Data.NetId);
				Anticheat.Flag($"Rejected a SetColor identity mismatch: claimed object {netId}, expected {player.Data.NetId}. Possible spoofing or desynchronization; sender unverified, no player penalized.");
				return false;
			}

			if(color >= Palette.ColorNames.Length)
			{
				Anticheat.Flag($"Rejected invalid color {color} for object {player.NetId}; sender unverified, no player penalized.");
				return false;
			}

			return true;
		}

		public override RpcCalls GetId()
		{
			return RpcCalls.SetColor;
		}

		public override bool IsHostOnly()
		{
			return true;
		}
	}
}
