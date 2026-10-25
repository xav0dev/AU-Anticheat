using System;
using Hazel;

namespace AUAnticheat.anticheat
{
	internal static class PacketPreflight
	{
		public static long BlockedPackets { get; private set; }
		public static string LastBlockReason { get; private set; } = "None";
		private static DateTime lastNoticeUtc;

		public static bool ValidateBatch(MessageReader reader, bool rejectUnknownTags)
		{
			try
			{
				if(!PacketRules.ValidateBatch(CopyRemaining(reader), rejectUnknownTags, out string reason)) return Block(reason);
				return true;
			}
			catch(Exception) { return Block("invalid packet buffer bounds"); }
		}

		public static bool ValidateRpc(RpcCalls rpc, MessageReader reader)
		{
			try
			{
				if(!PacketRules.ValidateRpc((byte)rpc, CopyRemaining(reader), out string reason)) return Block(reason);
				return true;
			}
			catch(Exception) { return Block("invalid RPC buffer bounds"); }
		}

		private static byte[] CopyRemaining(MessageReader reader)
		{
			if(reader == null || reader.Buffer == null) throw new FormatException();
			int remaining = reader.BytesRemaining;
			long start = (long)reader.Offset + reader.Position;
			if(remaining < 0 || remaining > 65535 || start < 0 || start + remaining > reader.Buffer.Length) throw new FormatException();
			byte[] copy = new byte[remaining];
			for(int i = 0; i < remaining; i++) copy[i] = reader.Buffer[(int)start + i];
			return copy;
		}

		public static bool Block(string reason)
		{
			LobbyChat.ProtectionNotice();
			SenderEvidence.Record(reason);
			BlockedPackets++;
			LastBlockReason = reason;
			DateTime now = DateTime.UtcNow;
			if((now - lastNoticeUtc).TotalSeconds >= 2)
			{
				lastNoticeUtc = now;
				AUAnticheatPlugin.Log.LogWarning($"Rejected packet before dispatch: {reason}");
				AUAnticheatPlugin.notifications.Send("Protection", $"Rejected packet before processing: {reason}. Sender unknown; no player penalized.");
			}
			return false;
		}

		public static void Reset()
		{
			BlockedPackets = 0;
			LastBlockReason = "None";
			lastNoticeUtc = DateTime.MinValue;
		}
	}
}
