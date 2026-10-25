using Hazel;
using InnerNet;
using System;

namespace AUAnticheat.anticheat.rpc
{
	internal class AddVote : RpcCheck
	{
		public override bool Validate(PlayerControl player, MessageReader reader)
		{
			int source = reader.ReadInt32();
			int target = reader.ReadInt32();

			ClientData client = AmongUsClient.Instance.FindClientById(source);
			ClientData targetClient = AmongUsClient.Instance.FindClientById(target);
			if(client == null || client.Character == null || client.Character.Data == null)
			{
				AUAnticheatPlugin.Log.LogInfo($"An unknown client id ({source}) attempted to votekick {target}");
				return false;
			}

			if(targetClient == null)
			{
				AUAnticheatPlugin.Log.LogInfo($"{client.Character.Data.PlayerName} attempted to votekick an unknown client id ({target})");
				return false;
			}

			// VoteBanSystem is the normal lobby-wide vote-kick mechanism. It is valid
			// outside meetings and remains available to dead players, so neither state
			// is evidence of cheating.
			return true;
		}

		public override RpcCalls GetId()
		{
			return RpcCalls.AddVote;
		}

		public override Type GetExpectedNetObject()
		{
			return typeof(VoteBanSystem);
		}
	}
}
