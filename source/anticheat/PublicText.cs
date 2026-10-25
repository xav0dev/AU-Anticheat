using System.Text;

namespace AUAnticheat.anticheat
{
	internal static class PublicText
	{
		public static string Clean(string text, int limit)
		{
			var result = new StringBuilder();
			foreach(char c in text ?? "")
			{
				if(result.Length >= limit) break;
				if(!char.IsControl(c) && !char.IsSurrogate(c) && c != '<' && c != '>' && c != '[' && c != ']') result.Append(c);
			}
			return result.ToString().Trim();
		}
		public static string Name(string text) { string name = Clean(text, 22); return name.Length == 0 ? "Unnamed player" : name; }
		public static string Simple(string title, string message)
		{
			if(message.Contains("several unexpected disconnects"))
				return "Several players disconnected together. This could be an attack or a connection problem. No attacker has been identified.";
			if(title.StartsWith("Anti-cheat"))
			{
				string lower = message.ToLowerInvariant();
				string category = lower.Contains("scan") ? "scan animation" :
					lower.Contains("vent") ? "vent action" :
					lower.Contains("movement") || lower.Contains("units/s") ? "movement" :
					lower.Contains("murder") || lower.Contains("kill") ? "kill action" :
					lower.Contains("meeting") || lower.Contains("vote") || lower.Contains("report") ? "meeting action" :
					lower.Contains("task") ? "task action" : lower.Contains("role") ? "role action" :
					lower.Contains("sabotage") || lower.Contains("system") ? "ship system action" : "game action";
				return $"Unusual {category}. Review the details; a named target is not proof of who sent it.";
			}
			if(title == "Protection" && message.StartsWith("Rejected packet before processing:"))
			{
				string action = message.Contains("vote-kick") ? "a vote-kick request" :
					message.Contains("Influencer") ? "an invalid or excessive Influencer message" :
					message.Contains("teleport") ? "an unexpected teleport" :
					message.Contains("vot") || message.Contains("meeting") ? "invalid meeting data" : "invalid game data";
				return $"Blocked {action} on this client. The sender is not verified, so nobody was automatically banned.";
			}
			return message;
		}
	}
}
