using HarmonyLib;
using Hazel;
using InnerNet;

namespace AUAnticheat.features
{
	internal static class Protections
	{
		public static bool BlockLargeGameMessages { get; set; } = true;
		public static bool BlockInvalidGameDataMessages { get; set; } = true;
		public static bool ProtectAgainstNonHostKickExploit { get; set; } = true;
		public static bool PreflightPackets { get; set; } = true;
		public static bool FilterVoteKickReplays { get; set; } = true;

		[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.SetEndpoint))]
		public static class ForceDtls
		{
			public static bool Enabled { get; set; } = true;

			private static void Prefix(ref bool dtls)
			{
				if(Enabled) dtls = true;
			}
		}

		[HarmonyPatch(typeof(CustomNetworkTransform), nameof(CustomNetworkTransform.HandleRpc))]
		public static class BlockUnexpectedLocalTeleport
		{
			public static bool Enabled { get; set; } = true;

			private static bool Prefix(CustomNetworkTransform __instance, byte callId)
			{
				if(!Enabled || __instance == null || callId != (byte)RpcCalls.SnapTo || __instance.myPlayer != PlayerControl.LocalPlayer) return true;
				if(Utilities.IsAnticheatPresent()) return true;
				PlayerControl local = PlayerControl.LocalPlayer;
				if(AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost || !anticheat.MovementContext.StableLivingPlayer(local)) return true;

				using var evidence = anticheat.SenderEvidence.Begin("Local teleport", callId, local.NetId, local.OwnerId, anticheat.PublicText.Name(local.Data.PlayerName));
				anticheat.SenderEvidence.Record();
				AUAnticheatPlugin.notifications.Send("Protection", "Blocked an unsolicited position change targeting your player.");
				return false;
			}
		}

		[HarmonyPatch(typeof(MessageReader), nameof(MessageReader.ReadPackedUInt32))]
		public static class HardenedReadPackedUInt
		{
			public static bool Enabled { get; set; } = true;

			private static bool Prefix(MessageReader __instance, ref uint __result)
			{
				if(!Enabled) return true;

				uint output = 0;
				for(int index = 0; index < 5; index++)
				{
					if(__instance.BytesRemaining < 1)
					{
						throw new System.FormatException("Truncated packed integer.");
					}

					byte value = __instance.ReadByte();
					if(index == 4 && (value & 0xf0) != 0) throw new System.FormatException("Packed integer overflow.");
					output |= (uint)(value & 0x7f) << (index * 7);
					if((value & 0x80) == 0)
					{
						__result = output;
						return false;
					}
				}

				throw new System.FormatException("Overlong packed integer.");
			}
		}

		[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.HandleRpc))]
		public static class MemoryAllocationOverload
		{
			public static bool Enabled { get; set; } = true;

			private static bool Prefix(byte callId, MessageReader reader)
			{
				if(!Enabled || callId != (byte)RpcCalls.VotingComplete) return true;

				int oldPosition = reader.Position;
				try
				{
					int arrayLength = reader.ReadPackedInt32();
					if(arrayLength < 0 || arrayLength > 1024 || arrayLength > reader.BytesRemaining)
					{
						AUAnticheatPlugin.notifications.Send("Protection", "Blocked a malformed voting result designed to exhaust memory.");
						return false;
					}
				}
				catch(System.Exception)
				{
					return anticheat.PacketPreflight.Block("malformed voting-result length");
				}
				finally
				{
					reader.Position = oldPosition;
				}

				return true;
			}
		}

		[HarmonyPatch(typeof(VoteBanSystem), nameof(VoteBanSystem.AddVote))]
		private static class VoteKickNotice
		{
			private static void Prefix(int srcClient, int clientId)
			{
				if(PlayerControl.LocalPlayer == null || clientId != PlayerControl.LocalPlayer.OwnerId) return;

				ClientData voter = AmongUsClient.Instance.FindClientById(srcClient);
				string name = voter == null ? "A player" : voter.PlayerName;
				AUAnticheatPlugin.notifications.Send("Vote-kick", $"A vote to remove you claims voter {name} (lobby ID {srcClient}). Sender unverified; this is not proof against that player.");
			}
		}
	}
}
