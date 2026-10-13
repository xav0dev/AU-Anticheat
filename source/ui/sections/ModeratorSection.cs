using UnityEngine;
using System.Collections.Generic;

namespace AUAnticheat.ui.sections
{
	internal class ModeratorSection : ISection
	{
		private int selectedOwnerId = -1;
		private uint selectedNetId;
		private string pendingAction;
		
		private string session;
		private string minimumLevelInput = "10";
		private bool showLevelKeypad;

		public ModeratorSection() : base("Players") { }

		public override void Render()
		{
			string current = AmongUsClient.Instance == null ? "offline" : $"{AmongUsClient.Instance.GameId}:{AmongUsClient.Instance.HostId}";
			if(current != session) { selectedOwnerId = -1; pendingAction = null; session = current; }
			bool host = AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;
			Styles.Help(host ? "Select a player to moderate. Names use their visible in-game color." : "Player list. Only the host can remove players.");
			PlayerControl target = FindSelectedPlayer();
			if(target == null) pendingAction = null;
			if(target != null)
			{
				Styles.HeadingText("Selected player");
				GUILayout.Label(PlayerPresentation.Name(target), PlayerPresentation.NameStyle(target, true));
				GUILayout.Label(PlayerPresentation.Details(target));
				Styles.Help("Game-reported details, not verified identity. Visible only to you.");
				ModerationTools.Selected(target);
				Styles.Help("Kick removes a player now. Ban also blocks ordinary rejoining of this lobby; it is not a game-wide account ban. Both require confirmation and host authority.");
			}
			if(target != null && host && target != PlayerControl.LocalPlayer && target.OwnerId != AmongUsClient.Instance.HostId)
			{
				GUILayout.BeginHorizontal();
				try
				{
					if(GUILayout.Button("Kick selected player")) pendingAction = "Kick";
					if(GUILayout.Button("Ban selected player", Styles.Danger)) pendingAction = "Ban";
				}
				finally { GUILayout.EndHorizontal(); }
				if(GUILayout.Button("Save persistent ban", Styles.Danger)) pendingAction = "Save persistent ban";
				if(pendingAction != null)
				{
					Styles.Help(pendingAction == "Save persistent ban" ?
						$"Save {PlayerPresentation.Name(target)} to your local ban list? Applies in this and future waiting lobbies while you host and list enforcement is on. Not a cheating accusation." :
						$"{pendingAction} {PlayerPresentation.Name(target)} from this lobby?");
					if(GUILayout.Button("Confirm " + pendingAction, Styles.Danger))
					{
						if(pendingAction == "Save persistent ban")
						{
							anticheat.ModerationStore.Set(anticheat.ChatModeration.BoundCode(target),"ban",true);
							AUAnticheatPlugin.notifications.Send("Local ban list",anticheat.ModerationStore.Status);
						}
						else
						{
							bool ban = pendingAction == "Ban";
							AmongUsClient.Instance.KickPlayer(target.OwnerId,ban);
							AUAnticheatPlugin.notifications.Send("Moderation", $"{PlayerPresentation.Name(target)}: {(ban ? "ban" : "kick")} requested by the host.");
						}
						pendingAction = null;
					}
					if(GUILayout.Button("Cancel")) pendingAction = null;
				}
			}
			if(!host) pendingAction = null;
			Styles.HeadingText("Choose a player");
			Styles.Help("Tap a name. Their details open at the top of this panel.");
			var roster = new List<PlayerControl>();
			foreach(var player in PlayerControl.AllPlayerControls)
				if(player != null && player.Data != null && !player.Data.Disconnected) roster.Add(player);
			for(int row = 0; row < roster.Count; row += 3)
			{
				GUILayout.BeginHorizontal();
				try
				{
					for(int i = row; i < System.Math.Min(row + 3, roster.Count); i++)
					{
						var player = roster[i];
						bool selected = player.OwnerId == selectedOwnerId && player.NetId == selectedNetId;
						string suffix = player == PlayerControl.LocalPlayer ? "  (you)" : player.OwnerId == AmongUsClient.Instance?.HostId ? "  (host)" : "";
						if(GUILayout.Button((selected ? ">  " : "") + PlayerPresentation.Name(player) + suffix, PlayerPresentation.NameStyle(player, selected)))
						{ selectedOwnerId = player.OwnerId; selectedNetId = player.NetId; pendingAction = null; scrollVector = new Vector2(0,0); }
					}
				}
				finally { GUILayout.EndHorizontal(); }
			}
			if(roster.Count == 0) Styles.Help("Players appear here when you join a lobby.");
			
			
		}

		internal void RenderLevelSelector()
		{
			anticheat.LobbyLevelPolicy.Enabled = Styles.Toggle(anticheat.LobbyLevelPolicy.Enabled, "Ban below minimum level");
			GUILayout.Label($"Minimum displayed level: {anticheat.LobbyLevelPolicy.MinimumLevel} (host always exempt)");
			Styles.Help("Uses the loaded live level, or the current join level while the live value is unavailable. Level 1 needs an observed level update, complete data, no earlier higher value on this player object, and 5 stable seconds. An unresolved loading value is never banned just because time passed. Host and exceptions remain exempt; reported levels are not authenticated account levels.");
			GUILayout.BeginHorizontal();
			try
			{
				foreach(int step in new[] { -100, -10, -1, 1, 10, 100 })
					if(GUILayout.Button(step > 0 ? $"+{step}" : step.ToString()))
					{
						anticheat.LobbyLevelPolicy.MinimumLevel = anticheat.LevelSelectorRules.Adjust(anticheat.LobbyLevelPolicy.MinimumLevel, step);
						minimumLevelInput = anticheat.LobbyLevelPolicy.MinimumLevel.ToString();
					}
			}
			finally { GUILayout.EndHorizontal(); }
			if(GUILayout.Button(showLevelKeypad ? "Close exact-level keypad" : "Choose exact minimum..."))
			{
				showLevelKeypad = !showLevelKeypad;
				minimumLevelInput = anticheat.LobbyLevelPolicy.MinimumLevel.ToString();
			}
			if(showLevelKeypad)
			{
				// The game's stripped GUI.DoTextField cannot be restored by interop.
				// Use existing supported label/button controls; no native text editor.
				GUILayout.Label($"Pending minimum: {(minimumLevelInput.Length == 0 ? "(empty)" : minimumLevelInput)}");
				for(int row = 0; row < 2; row++)
				{
					GUILayout.BeginHorizontal();
					try
					{
						for(int digit = row * 5; digit < row * 5 + 5; digit++)
							if(GUILayout.Button(digit.ToString())) minimumLevelInput = anticheat.LevelSelectorRules.Append(minimumLevelInput, digit);
					}
					finally { GUILayout.EndHorizontal(); }
				}
				GUILayout.BeginHorizontal();
				try
				{
					if(GUILayout.Button("Clear")) minimumLevelInput = "";
					if(GUILayout.Button("Backspace")) minimumLevelInput = anticheat.LevelSelectorRules.Backspace(minimumLevelInput);
					if(GUILayout.Button("Apply minimum") && anticheat.LevelSelectorRules.TryMinimum(minimumLevelInput, out uint minimum))
						anticheat.LobbyLevelPolicy.MinimumLevel = minimum;
				}
				finally { GUILayout.EndHorizontal(); }
				Styles.Help("Enter a whole number, then Apply. Minimum: 1.");
			}
			Styles.Help("Can be configured with players already here. Host only: waits for the game's ban permission and matching client records, then bans one at a time with a departure wait and 2-second cooldown. No entry-rule chat messages. Host and friend-code exceptions are exempt; levels are not proof of cheating.");
		}

		private PlayerControl FindSelectedPlayer()
		{
			foreach(PlayerControl player in PlayerControl.AllPlayerControls)
			{
				if(player != null && player.Data != null && !player.Data.Disconnected && player.OwnerId == selectedOwnerId && player.NetId == selectedNetId) return player;
			}

			return null;
		}
	}
}
