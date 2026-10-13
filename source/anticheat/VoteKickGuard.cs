using System.Diagnostics;
using Hazel;

namespace AUAnticheat.anticheat
{
	internal static class VoteKickGuard
	{
		private static readonly VoteReplayWindow RecentVotes = new();
		public static long RejectedVotes { get; private set; }
		public static void Reset() { RecentVotes.Reset(); RejectedVotes = 0; }
		public static void Forget(int clientId) => RecentVotes.Forget(clientId);

		public static bool Validate(MessageReader reader, System.Func<int,string> resolveName = null)
		{
			if(reader == null) return Reject("missing vote-kick payload");
			int oldPosition = reader.Position;
			try
			{
				if(reader.BytesRemaining < 8) return Reject("truncated vote-kick pair");
				int source = reader.ReadInt32(), target = reader.ReadInt32();
				string targetName = resolveName?.Invoke(target);
				SenderEvidence.NoteTarget($"vote-kick target {targetName ?? "unknown player"} (lobby ID {target})");
				SenderEvidence.NoteCandidate(source, 0);
				var client = AmongUsClient.Instance;
				if(client == null) return true;
				if(source == target) return Reject("self-targeted vote-kick");
				if(client.FindClientById(source) == null || client.FindClientById(target) == null)
					return Reject("vote-kick references a client absent from this lobby");
				double now = (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;
				return RecentVotes.Accept(source, target, now) || Reject("duplicate vote-kick pair within two seconds");
			}
			catch(System.Exception) { return Reject("invalid vote-kick payload or unavailable client state"); }
			finally { reader.Position = oldPosition; }
		}

		private static bool Reject(string reason)
		{
			RejectedVotes++;
			return PacketPreflight.Block(reason);
		}

	}
}
