using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AUAnticheat.anticheat
{
	internal static class FriendCodeExceptions
	{
		private static readonly SortedSet<string> Codes = new(StringComparer.Ordinal);
		private static Action<string> save;
		public static string[] Snapshot() => Codes.ToArray();
		public static void Clear() { Codes.Clear(); save?.Invoke(""); }
		public static string Normalize(string text)
		{
			string code = (text ?? "").Trim().ToLowerInvariant();
			return code.Length <= 36 && Regex.IsMatch(code, "^[a-z0-9]{1,24}#[0-9]{1,10}$") ? code : null;
		}
		public static void Initialize(string stored, Action<string> persist)
		{
			Codes.Clear(); save = persist;
			foreach(string part in (stored ?? "").Split(';'))
			{
				string normalized = Normalize(part);
				if(normalized != null && Codes.Count < 128) Codes.Add(normalized);
			}
		}
		public static bool Contains(string code) { string value = Normalize(code); return value != null && Codes.Contains(value); }
		public static bool Add(string code)
		{
			string value = Normalize(code);
			if(value == null || Codes.Count >= 128 || !Codes.Add(value)) return false;
			save?.Invoke(string.Join(";", Codes)); return true;
		}
		public static bool Remove(string code)
		{
			string value = Normalize(code);
			if(value == null || !Codes.Remove(value)) return false;
			save?.Invoke(string.Join(";", Codes)); return true;
		}
	}
}
