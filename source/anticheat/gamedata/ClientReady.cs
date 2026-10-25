using AmongUs.InnerNet.GameDataMessages;
using Hazel;
using InnerNet;

namespace AUAnticheat.anticheat.gamedata
{
	internal class ClientReady : GameDataCheck
	{
		public override bool Validate(MessageReader reader)
		{
			int clientId = reader.ReadPackedInt32();

			ClientData client = AmongUsClient.Instance.FindClientById(clientId);
			if(client == null)
			{
				Anticheat.Flag($"Received ClientReady message for unknown client: {clientId}.");
				return false;
			}

			// Ready messages are idempotent and may legitimately be repeated while a
			// client reconnects or retransmits during scene loading.
			return true;
		}

		public override GameDataTypes GetId()
		{
			return GameDataTypes.ReadyFlag;
		}
	}
}
