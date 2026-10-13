using Hazel;

namespace AUAnticheat.anticheat.rpc
{
	internal class SetName : RpcCheck
	{
		// We increase the max name length to 12 instead of 10 as we need to account for the numbers at the end of player names when there are multiple players with the same name
		public readonly int MAX_NAME_LENGTH = 12;

		public override bool Validate(PlayerControl player, MessageReader reader)
		{
			// On modded lobbies, it is common for players to have custom names
			if(!Utilities.IsAnticheatPresent()) return true;

			uint netId = reader.ReadUInt32();
			string requestedName = reader.ReadString();

			if(netId != GetExpectedNetId(player))
			{
				SenderEvidence.NoteObjectClaim(netId, GetExpectedNetId(player));
				Anticheat.Flag($"Rejected a SetName identity mismatch: claimed object {netId}, expected {GetExpectedNetId(player)}. Possible spoofing or desynchronization; sender unverified, no player penalized.");
				return false;
			}

			if(requestedName.Length > MAX_NAME_LENGTH)
			{
				Anticheat.Flag($"Rejected an overlong claimed name ({requestedName.Length} characters). Affected object {player.NetId}; sender unverified, no player penalized.");
				return false;
			}

			if(requestedName.Contains('<'))
			{
				Anticheat.Flag($"Rejected a claimed name containing markup. Affected object {player.NetId}; sender unverified, no player penalized.");
				return false;
			}

			return true;
		}

		private uint GetExpectedNetId(PlayerControl player)
		{
			// In host authority, the host sends the SetName RPC with the net id of the player's NetworkedPlayerInfo net object
			// In server authority, the server sends the SetName RPC with the net id of the player's PlayerControl net object
			// I don't know why this discrepancy exists, or why the SetName RPC even includes the net id field
			return Utilities.IsAnticheatPresent() ? player.NetId : player.Data.NetId;
		}

		public override RpcCalls GetId()
		{
			return RpcCalls.SetName;
		}

		// On vanilla servers, the host is able to receive the SetName RPC due to how server-authority works
		// On modded servers, the host should never receive the SetName RPC
		public override bool IsHostOnly()
		{
			return !Utilities.IsAnticheatPresent();
		}
	}
}
