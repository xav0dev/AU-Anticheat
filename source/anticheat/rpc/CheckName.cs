using Hazel;

namespace AUAnticheat.anticheat.rpc
{
	internal class CheckName : RpcCheck
	{
		public readonly int MAX_NAME_LENGTH = 10;

		public override bool Validate(PlayerControl player, MessageReader reader)
		{
			string requestedName = reader.ReadString();

			/*
			ClientData clientData = AmongUsClient.Instance.GetClient(player.OwnerId);
			if(AmongUsClient.Instance.NetworkMode != NetworkModes.LocalGame && requestedName != clientData.PlayerName)
			{
				Anticheat.Flag(player, $"{clientData.PlayerName} requested a name that does not match their name in the login handshake.");
				player.SetName(clientData.PlayerName);
				return false;
			}
			*/

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

		public override RpcCalls GetId()
		{
			return RpcCalls.CheckName;
		}
	}
}
