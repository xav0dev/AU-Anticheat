using System;
using System.Collections.Generic;

namespace AUAnticheat.anticheat
{
	internal sealed class RoundLedger
	{
		private sealed class Player { public string Name, Role; public bool Impostor; public int Kills; }
		private readonly Dictionary<uint, Player> players = new();
		private readonly HashSet<uint> killed = new();
		private double started, duration;
		private int reason;
		private int completedTasks = -1, totalTasks = -1;
		public bool Running { get; private set; }
		public bool Completed { get; private set; }
		public void Begin(double now) { players.Clear(); killed.Clear(); completedTasks = totalTasks = -1; started = now; Running = true; Completed = false; }
		public void Tasks(int done, int total) { if(Running && done >= 0 && total >= done && total <= 10000) { completedTasks = done; totalTasks = total; } }
		public string MultilineSummary(int budget)
		{
			if(!Completed) return null;
			var imps = new List<Player>();
			foreach(var p in players.Values) if(p.Impostor) imps.Add(p);
			if(imps.Count == 0) return null;
			for(int limit = 22; limit >= 3; limit--)
			{
				var lines = new List<string> { Winner(reason) + " Win" };
				foreach(var p in imps) lines.Add((p.Name.Length <= limit ? p.Name : p.Name.Substring(0,limit) + "~") + $": {p.Kills} kill" + (p.Kills == 1 ? "" : "s"));
				if(totalTasks >= 0) lines.Add($"Task: {completedTasks}/{totalTasks}");
				lines.Add($"Match Time: {(int)duration / 60:00}:{(int)duration % 60:00}");
				string message = string.Join("\n", lines);
				if(message.Length <= budget) return message;
			}
			return ChatSummary(budget);
		}
		public void Observe(uint id, string name, string role, bool impostor)
		{
			if(!Running) return;
			if(!players.TryGetValue(id, out var player))
			{
				if(players.Count >= 64) return;
				players[id] = player = new Player();
			}
			player.Name = PublicText.Name(name);
			// Default crew records during transitions must not erase observed roles.
			if(!player.Impostor || impostor) player.Role = PublicText.Clean(role, 24);
			player.Impostor |= impostor;
		}
		public void Kill(uint killer, uint victim)
		{
			if(!Running || killer == victim || killed.Count >= 64 || !players.TryGetValue(killer, out var source) || !source.Impostor || !killed.Add(victim)) return;
			source.Kills++;
		}
		public void End(double now, int outcome)
		{
			if(!Running) return;
			duration = double.IsFinite(now - started) ? Math.Clamp(now - started, 0, 86400) : 0;
			reason = outcome; Running = false; Completed = true;
		}
		public static string Winner(int outcome) => outcome switch { 0 or 1 or 5 or 7 => "Crewmates", 2 or 3 or 4 or 6 or 8 => "Impostors", _ => "Unknown" };
        public string ChatSummary(int budget)
        {
            if(!Completed || budget <= 0) return null;
            string header = $"{Winner(reason)} won {(int)duration / 60}:{(int)duration % 60:00}";
            var impostors = new List<Player>();
            foreach(var player in players.Values) if(player.Impostor) impostors.Add(player);
            if(impostors.Count == 0)
            {
                return null; // Keep incomplete results local; never invent missing roles.
            }
            // Keep every impostor and kill count. Only abbreviate displayed names
            // when needed; ~ marks shortened names. Full details stay in Alerts.
            for(int nameLimit = 22; nameLimit >= 1; nameLimit--)
            {
                var parts = new List<string>();
                foreach(var player in impostors)
                {
                    string name = player.Name.Length <= nameLimit ? player.Name : player.Name.Substring(0,nameLimit) + "~";
                    string role = player.Role switch { "Impostor" => "I", "Shapeshifter" => "S", "Phantom" => "P", "Viper" => "V", _ => "?" };
                    parts.Add($"{name}({role}):{player.Kills}k");
                }
                string message = header + " | " + string.Join("; ",parts);
                if(message.Length <= budget) return message;
            }
            return null; // Cannot represent an unusual roster without dropping stats.
        }
		public List<string> Lines()
		{
			var lines = new List<string>();
			if(!Completed) return lines; // Never leak roles from an active game.
			lines.Add($"{Winner(reason)} won | {(int)duration / 60}m {(int)duration % 60}s");
			if(totalTasks >= 0) lines.Add($"Tasks: {completedTasks}/{totalTasks}");
			foreach(var player in players.Values)
				if(player.Impostor && lines.Count < 16) lines.Add($"{player.Name} ({player.Role}): {player.Kills} kill(s)");
			if(!new List<Player>(players.Values).Exists(p => p.Impostor)) lines.Add("No impostor role data was available to this client.");
			return lines;
		}
	}
}
