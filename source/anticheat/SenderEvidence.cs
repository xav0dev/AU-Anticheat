using System;
using System.Collections.Generic;
using System.Text;

namespace AUAnticheat.anticheat
{
	// Neither an object owner nor a payload claim authenticates the sender.
	// Repetition cannot upgrade these observations into transport evidence.
	internal static class SenderEvidence
	{
		internal enum Basis { RelayedMessage, ReplicatedState, PlatformMetadata, Unknown }
		internal sealed class Context : IDisposable
		{
			internal readonly Basis EvidenceBasis;
			internal readonly string Channel;
			internal readonly byte? Rpc;
			internal readonly uint? ObjectId;
			internal readonly int? ObjectOwner;
			internal readonly string Procedure;
			internal bool SequenceRecorded;
			internal int? ClaimedActor;
			internal int ProposedPoints;
			internal int OwnerPolicyPoints;
			internal bool ConflictingClaims;
			internal string Affected;
			private readonly Context previous;
			internal readonly int generation;
			private bool disposed;
			internal Context(string channel, byte? rpc, uint? objectId, int? owner, string affected, string procedure, Basis basis)
			{
				EvidenceBasis = basis;
				Procedure = procedure;
				Channel = channel; Rpc = rpc; ObjectId = objectId; ObjectOwner = owner; Affected = affected;
				previous = Current; generation = sessionGeneration; Current = this;
			}
			public void Dispose()
			{
				if(disposed) return;
				disposed = true;
				if(generation == sessionGeneration && ReferenceEquals(Current, this))
				{
					var restore = previous;
					while(restore != null && (restore.disposed || restore.generation != sessionGeneration)) restore = restore.previous;
					Current = restore;
				}
			}
		}
		internal sealed class Entry
		{
			internal Basis EvidenceBasis;
			internal string Reason, Procedure;
			internal DateTime TimeUtc;
			internal string Channel;
			internal byte? Rpc;
			internal uint? ObjectId;
			internal int? ObjectOwner, ClaimedActor;
			internal int ProposedPoints;
			internal int OwnerPolicyPoints;
			internal bool ConflictingClaims;
			internal int Repeats = 1;
			internal string Affected;
		}
		[ThreadStatic] internal static Context Current;
		private static volatile int sessionGeneration;
		private static readonly object Sync = new();
		private static readonly Queue<Entry> Entries = new();
		private static Entry latest;
		private static readonly SequenceEvidence Sequences = new();
		public static int Count { get { lock(Sync) return Entries.Count; } }
		public static Context Begin(string channel, byte? rpc, uint? objectId, int? owner, string affected = null, string procedure = null, Basis basis = Basis.RelayedMessage)
			=> new Context(channel, rpc, objectId, owner, affected, procedure, basis);
		// This is an explicitly selected moderation heuristic, NOT authentication.
		// It only authorizes scoring the live PlayerControl object referenced by an RPC.
		internal static bool CanUseReferencedOwner(bool enabled, bool host, bool localPlayer,
			bool hostPlayer, bool liveBinding, bool ambiguous, int ownerId, uint netId)
		{
			var context = Current;
			return enabled && host && !localPlayer && !hostPlayer && liveBinding && !ambiguous &&
				ownerId >= 0 && context != null && context.generation == sessionGeneration &&
				context.EvidenceBasis == Basis.RelayedMessage && context.Channel == "PlayerControl" &&
				context.Rpc.HasValue && context.ObjectOwner == ownerId && context.ObjectId == netId &&
				!context.ConflictingClaims && (!context.ClaimedActor.HasValue || context.ClaimedActor == ownerId);
		}
		internal static void NoteOwnerPolicyPoints(int points)
		{
			if(Current != null) Current.OwnerPolicyPoints = Math.Clamp(points, 0, 100);
		}
		public static void PauseSequences(int? owner, double seconds) { lock(Sync) Sequences.Pause(owner, seconds); }
		public static void ForgetSequences(int owner) { lock(Sync) Sequences.Forget(owner); }
		public static string SequenceSummary(Func<int, string> name = null) { lock(Sync) return Sequences.Summary(name); }
		public static void NoteTarget(string affected) { if(Current != null) Current.Affected = affected; }
		public static string TargetSummary => "Affected: " + (Current?.Affected ?? "unknown / not identifiable from this message") + ".";
		public static void NoteCandidate(int? claimedActor, int points)
		{
			if(Current == null) return;
			if(Current.ClaimedActor.HasValue && claimedActor.HasValue && Current.ClaimedActor != claimedActor)
				Current.ConflictingClaims = true;
			Current.ClaimedActor ??= claimedActor;
			Current.ProposedPoints = Math.Max(Current.ProposedPoints, Math.Clamp(points, 0, 100));
			if(Current.ObjectOwner.HasValue && claimedActor.HasValue && Current.ObjectOwner != claimedActor)
				Current.ConflictingClaims = true;
		}
		public static void NoteObjectClaim(uint claimed, uint expected)
		{
			if(Current != null && claimed != expected) Current.ConflictingClaims = true;
		}
		public static void Record(string reason = null)
		{
			lock(Sync)
			{
				var context = Current;
				try { RecordCore(reason); }
				finally { if(context != null) context.OwnerPolicyPoints = 0; }
			}
		}
		private static void RecordCore(string reason)
		{
			var context = Current != null && Current.generation == sessionGeneration ? Current : null;
			reason = SafeDetail(reason, 320);
			if(context != null && !context.SequenceRecorded && context.EvidenceBasis == Basis.RelayedMessage)
			{
				context.SequenceRecorded = true;
				Sequences.Observe(context.ObjectOwner, context.ObjectId, context.ClaimedActor, context.ConflictingClaims, context.Procedure);
			}
			DateTime now = DateTime.UtcNow;
			string channel = context?.Channel ?? "Packet/unknown";
			if(latest != null && now >= latest.TimeUtc && (now - latest.TimeUtc).TotalSeconds < 2 &&
				latest.Channel == channel && latest.Rpc == context?.Rpc && latest.ObjectId == context?.ObjectId &&
				latest.ObjectOwner == context?.ObjectOwner && latest.ClaimedActor == context?.ClaimedActor &&
				latest.Affected == SafeDetail(context?.Affected, 160) && latest.Reason == reason &&
				latest.Procedure == context?.Procedure && latest.EvidenceBasis == (context?.EvidenceBasis ?? Basis.Unknown) &&
				latest.OwnerPolicyPoints == (context?.OwnerPolicyPoints ?? 0) &&
				latest.ConflictingClaims == (context?.ConflictingClaims ?? false) && latest.ProposedPoints == (context?.ProposedPoints ?? 0))
			{
				latest.Repeats = Math.Min(1000000, latest.Repeats + 1);
				return;
			}
			latest = new Entry { TimeUtc = now, Channel = channel, Rpc = context?.Rpc, ObjectId = context?.ObjectId,
				Reason = reason, Procedure = context?.Procedure, EvidenceBasis = context?.EvidenceBasis ?? Basis.Unknown,
				OwnerPolicyPoints = context?.OwnerPolicyPoints ?? 0,
				ObjectOwner = context?.ObjectOwner, ClaimedActor = context?.ClaimedActor,
				ConflictingClaims = context?.ConflictingClaims ?? false, ProposedPoints = context?.ProposedPoints ?? 0, Affected = SafeDetail(context?.Affected, 160) };
			while(Entries.Count >= 100) Entries.Dequeue();
			Entries.Enqueue(latest);
		}
		public static void Reset()
		{
			lock(Sync) { sessionGeneration++; Current = null; Entries.Clear(); latest = null; Sequences.Clear(); }
		}
		public static string Summary(int maximum = 10)
		{
			lock(Sync) return SummaryCore(maximum);
		}
		private static string SummaryCore(int maximum)
		{
			var output = new StringBuilder();
			var rows = Entries.ToArray();
			for(int i = Math.Max(0, rows.Length - Math.Clamp(maximum, 0, 100)); i < rows.Length; i++)
			{
				var row = rows[i];
				output.AppendLine($"{row.TimeUtc:HH:mm:ss} UTC | {row.Channel} RPC {row.Rpc?.ToString() ?? "?"} | object {row.ObjectId?.ToString() ?? "?"}");
				output.AppendLine("Evidence basis: " + BasisLabel(row.EvidenceBasis) + ". No authenticated original sender available.");
				if(row.Procedure != null) output.AppendLine("Procedure: " + SafeDetail(row.Procedure, 64));
				if(row.Reason != null) output.AppendLine("Observed: " + row.Reason);
				output.AppendLine($"Sender UNKNOWN; object owner {row.ObjectOwner?.ToString() ?? "?"}; claimed actor {row.ClaimedActor?.ToString() ?? "?"}. These are not verified senders.");
				output.AppendLine("Affected: " + (row.Affected ?? "unknown / not identifiable from this message") + ".");
				if(row.OwnerPolicyPoints > 0)
					output.AppendLine($"Referenced-owner policy: {row.OwnerPolicyPoints} points applied per recorded occurrence ({row.Repeats} occurrence(s)). This is a spoofable owner association, NOT verified sender identity.");
				else output.AppendLine($"{(row.ConflictingClaims ? "Conflicting claims; " : "")}{row.Repeats} observation(s); {row.ProposedPoints} candidate points, NOT applied.");
			}
			return output.ToString();
		}
		private static string BasisLabel(Basis basis) => basis switch
		{
			Basis.RelayedMessage => "relayed message / object reference",
			Basis.ReplicatedState => "replicated state, not a sender identity",
			Basis.PlatformMetadata => "reported platform metadata, not a device check",
			_ => "unknown"
		};
		private static string SafeDetail(string value, int maximum)
		{
			if(value == null) return null;
			var result = new StringBuilder();
			foreach(char c in value)
			{
				if(result.Length >= maximum) break;
				if(char.IsControl(c) || char.IsSurrogate(c) || char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format) continue;
				if(c != '<' && c != '>') result.Append(c);
			}
			return result.ToString();
		}
	}
}
