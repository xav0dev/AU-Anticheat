using AmongUs.InnerNet.GameDataMessages;
using HarmonyLib;
using Hazel;
using AUAnticheat.anticheat;
using AUAnticheat.features;
using InnerNet;

namespace AUAnticheat.network
{
	internal class DataHandler
	{
		// On Vanilla servers, messages are limited to at most 1201 bytes
		// On Modded Nikocat servers, messages are limited to at most 1400 bytes
		// Messages shouldn't greater than 1500 bytes anyway, as at that point messages will likely not be received by some clients due to exceeding MTU size
		// 1400 bytes seems to be the sweet spot, as it is enough to support modded servers, and also enough to prevent attacks with large messages
		public static readonly int MAX_MESSAGE_LENGTH = 1400;
		public static readonly int MAX_NESTED_MESSAGES = 96;

		[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.HandleGameData))]
		class HandleGameData
		{
			static bool Prefix(InnerNetClient __instance, MessageReader parentReader)
			{
				if(Protections.BlockLargeGameMessages && parentReader.Length > MAX_MESSAGE_LENGTH)
				{
					PacketPreflight.Block("oversized game-data message");
					parentReader.Recycle();
					return false;
				}

				try
				{
					// Validate the complete envelope and supported RPC structures before
					// scheduling ANY child. A bad tail must not follow an executed prefix.
					if(Protections.PreflightPackets && !PacketPreflight.ValidateBatch(parentReader, Protections.BlockInvalidGameDataMessages)) return false;
					int nestedMessageCount = 0;
					while(parentReader.BytesRemaining > 0)
					{
						if(++nestedMessageCount > MAX_NESTED_MESSAGES)
						{
							PacketPreflight.Block("excessive nested message count");
							break;
						}

						MessageReader reader = parentReader.ReadMessageAsNewBuffer();
						HandleGameDataInner(__instance, reader, ++__instance.msgNum);
					}
				}
				catch(System.Exception)
				{
					PacketPreflight.Block("malformed nested game data");
				}
				finally
				{
					parentReader.Recycle();
				}

				return false;
			}
		}

		public static void HandleGameDataInner(InnerNetClient innerNetClient, MessageReader reader, int msgNum)
		{
			GameDataTypes type = (GameDataTypes)reader.Tag;

			if(Protections.BlockInvalidGameDataMessages && !PacketRules.KnownGameDataTag((byte)type))
			{
				PacketPreflight.Block("invalid game-data type");
				reader.Recycle();
				return;
			}

			bool isValid = Anticheat.HandleGameData(type, reader);
			if(!isValid)
			{
				reader.Recycle();
				return;
			}

			innerNetClient.StartCoroutine(innerNetClient.HandleGameDataInner(reader, msgNum));
		}
	}
}
