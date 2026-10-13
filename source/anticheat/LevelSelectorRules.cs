using System.Globalization;

namespace AUAnticheat.anticheat
{
	internal static class LevelSelectorRules
	{
		public static string Append(string input, int digit) => digit < 0 || digit > 9 || input.Length >= 10
			? input : input + digit.ToString(CultureInfo.InvariantCulture);
		public static string Backspace(string input) => input.Length == 0 ? input : input.Substring(0, input.Length - 1);
		public static bool TryMinimum(string input, out uint minimum) =>
			uint.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out minimum) && minimum > 0;
		public static uint Adjust(uint value, int delta) => (uint)System.Math.Clamp((long)value + delta, 1, uint.MaxValue);
	}
}
