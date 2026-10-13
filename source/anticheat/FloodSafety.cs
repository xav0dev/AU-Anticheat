using System;
using System.Collections.Generic;

namespace AUAnticheat.anticheat
{
	internal static class FloodSafety
	{
		// Above all existing burst thresholds. Retain recent evidence, never an
		// attacker-controlled number of records in one observation window.
		public const int MaximumSamples = 256;
		public static void Enqueue<T>(Queue<T> samples, T sample)
		{
			while(samples.Count >= MaximumSamples) samples.Dequeue();
			samples.Enqueue(sample);
		}
	}

	internal sealed class VoteReplayWindow
	{
		public const int MaximumPairs = 256;
		public const double ReplaySeconds = 2;
		private readonly Dictionary<(int Source, int Target), double> accepted = new();
		public int Count => accepted.Count;

		// A claimed voter is NOT an authenticated sender. This is idempotency
		// protection only, and must never add score or trigger a ban.
		public bool Accept(int source, int target, double now)
		{
			if(!double.IsFinite(now)) return true;
			var key = (source, target);
			if(accepted.TryGetValue(key, out double previous) && now >= previous && now - previous < ReplaySeconds) return false;
			if(!accepted.ContainsKey(key) && accepted.Count >= MaximumPairs)
			{
				// Evict the oldest record, not a different voter's new legitimate vote.
				var oldest = key;
				double oldestTime = double.PositiveInfinity;
				foreach(var pair in accepted)
					if(pair.Value < oldestTime) { oldest = pair.Key; oldestTime = pair.Value; }
				accepted.Remove(oldest);
			}
			accepted[key] = now;
			return true;
		}

		public void Forget(int clientId)
		{
			var remove = new List<(int Source, int Target)>();
			foreach(var pair in accepted)
				if(pair.Key.Source == clientId || pair.Key.Target == clientId) remove.Add(pair.Key);
			foreach(var key in remove) accepted.Remove(key);
		}
		public void Reset() => accepted.Clear();
	}
}
