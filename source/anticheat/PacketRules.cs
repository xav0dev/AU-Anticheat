using System;
using System.Text;

namespace AUAnticheat.anticheat
{
	// Pure managed rules, shared verbatim with the regression harness. Numeric IDs
	// match RpcCalls/GameDataTypes in the inspected v19s (build 7489) assemblies.
	internal static class PacketRules
	{
		private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

		public static bool TryPackedUInt(ReadOnlySpan<byte> bytes, ref int position, out uint value)
		{
			value = 0;
			for(int index = 0; index < 5; index++)
			{
				if(position < 0 || position >= bytes.Length) return false;
				byte next = bytes[position++];
				if(index == 4 && (next & 0xf0) != 0) return false;
				value |= (uint)(next & 0x7f) << (7 * index);
				if((next & 0x80) == 0) return true;
			}
			return false;
		}

		public static bool ValidString(ReadOnlySpan<byte> bytes, ref int position, int maximumBytes)
		{
			if(!TryPackedUInt(bytes, ref position, out uint length) || length > maximumBytes || length > bytes.Length - position) return false;
			try { StrictUtf8.GetCharCount(bytes.Slice(position, (int)length)); }
			catch(DecoderFallbackException) { return false; }
			position += (int)length;
			return true;
		}

		public static bool ValidateBatch(ReadOnlySpan<byte> bytes, bool rejectUnknownTags, out string reason)
		{
			int cursor = 0;
			int messages = 0;
			while(cursor < bytes.Length)
			{
				if(++messages > 96) return Fail("excessive nested-message count", out reason);
				if(bytes.Length - cursor < 3) return Fail("truncated nested-message header", out reason);
				int length = bytes[cursor] | (bytes[cursor + 1] << 8);
				byte tag = bytes[cursor + 2];
				cursor += 3;
				if(length > bytes.Length - cursor) return Fail("nested message extends past its packet", out reason);
				ReadOnlySpan<byte> child = bytes.Slice(cursor, length);
				int field = 0;
				if(rejectUnknownTags && !KnownGameDataTag(tag)) return Fail("unsupported nested-message tag", out reason);
				if(tag == 1 || tag == 2 || tag == 5 || tag == 6 || tag == 7)
				{
					if(!TryPackedUInt(child, ref field, out _)) return Fail("invalid nested object/client identifier", out reason);
				}
				if(tag == 2)
				{
					if(field >= child.Length) return Fail("missing RPC identifier", out reason);
					byte rpc = child[field++];
					if(!ValidateRpc(rpc, child.Slice(field), out reason)) return false;
				}
				if(tag == 6 && !ValidString(child, ref field, 256)) return Fail("invalid scene-name string", out reason);
				cursor += length;
			}
			reason = null;
			return true;
		}

		public static bool ValidateRpc(byte rpc, ReadOnlySpan<byte> bytes, out string reason)
		{
			int position = 0;
			bool valid;
			// Only verified required fields are checked. Unreviewed/custom RPCs and
			// trailing extension fields are left to the game, never called cheats.
				switch(rpc)
			{
				case 66: valid = bytes.Length >= 4; break; // Judge: actor, target, ushort nonce
				case 67:
					// Influencer: packed length followed by 1..3 image indices. Check
					// before the native array reader allocates or the feed renders.
					valid = TryPackedUInt(bytes, ref position, out uint images) &&
						images >= 1 && images <= 3 && images <= bytes.Length - position;
					break;
				case 1: case 19: case 20: case 34: case 38: case 47: case 48:
					valid = TryPackedUInt(bytes, ref position, out _); break;
				case 5: case 13: case 39: case 40: case 41: case 42: case 43:
					valid = ValidString(bytes, ref position, rpc == 13 ? 1600 : 512); break;
				case 6:
					position = 4;
					valid = bytes.Length >= 4 && ValidString(bytes, ref position, 512); break;
				case 0: case 7: case 11: case 14: case 27:
					valid = bytes.Length >= 1; break;
				case 8: valid = bytes.Length >= 5; break;
				case 12:
					valid = TryPackedUInt(bytes, ref position, out _) && bytes.Length - position >= 4; break;
				case 15: valid = bytes.Length >= 2 && bytes[0] <= 1; break;
				case 18:
					valid = TryPackedUInt(bytes, ref position, out _) && bytes.Length - position >= 1; break;
				case 21: valid = bytes.Length >= 6; break; // two encoded ushort coordinates + sequence
				case 24: case 31: valid = bytes.Length >= 2; break;
				case 26: valid = bytes.Length >= 8; break;
				case 35:
					position = 1;
					valid = bytes.Length >= 1 && TryPackedUInt(bytes, ref position, out _) && bytes.Length - position >= 1; break;
				case 44: valid = bytes.Length >= 3 && bytes[2] <= 1; break;
				case 46:
					valid = TryPackedUInt(bytes, ref position, out _) && position < bytes.Length && bytes[position] <= 1; break;
				case 55:
					valid = TryPackedUInt(bytes, ref position, out _) && (position == bytes.Length || bytes[position] <= 1); break;
				case 23:
					// At minimum every vote entry consumes one byte. Keep the existing
					// generous cap until the exact result layout is verified in-game.
					valid = TryPackedUInt(bytes, ref position, out uint count) && count <= 1024 && count <= bytes.Length - position; break;
				default: valid = true; break;
			}
			reason = valid ? null : $"malformed RPC {rpc} required fields";
			return valid;
		}

		public static bool KnownGameDataTag(byte tag) => tag == 1 || tag == 2 ||
			tag == 4 || tag == 5 || tag == 6 || tag == 7 || tag == 8 || tag == 207;

		private static bool Fail(string message, out string reason)
		{
			reason = message;
			return false;
		}
	}
}
