using System;

namespace AUAnticheat.anticheat
{
	internal static class InfluencerRules
	{
		public static bool ValidImageCount(int count) => count >= 1 && count <= 3;
		public static bool ValidImageIndex(int index, int spriteCount) => index >= 0 && index < spriteCount;
	}

	// Local resource ceiling, NOT a role cooldown or evidence against a player.
	// Fixed storage: rejected floods cannot grow a queue or attacker-keyed map.
	internal sealed class InfluencerBurstWindow
	{
		public const int MaximumMessages = 64;
		private double started = double.NaN;
		private int count;
		public void Reset() { started = double.NaN; count = 0; }
		public bool TryAccept(double now)
		{
			if(!double.IsFinite(now)) return false;
			if(double.IsNaN(started) || now < started || now - started >= 10)
			{
				started = now;
				count = 0;
			}
			if(count >= MaximumMessages) return false;
			count++;
			return true;
		}
	}
}
