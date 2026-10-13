using Hazel;

using InnerNet;

namespace AUAnticheat.anticheat.rpc
{
	internal class ReportDeadBody : RpcCheck
	{
		public override bool Validate(PlayerControl player, MessageReader reader)
		{
			// Current Among Us writes a player ID byte, not a NetworkedPlayerInfo net ID.
			// byte.MaxValue represents an emergency-button report with no body target.
			byte targetPlayerId = reader.ReadByte();
			if(player == null || player.Data == null)
			{
				Anticheat.Flag("Blocked ReportDeadBody without a valid player object.");
				return false;
			}

			// Another report may have started the meeting while this reliable request was
			// in flight. It is harmless and must not become evidence or a second meeting.
			if(MeetingHud.Instance != null || Anticheat.HasMeetingTransitionGrace())
			{
				AUAnticheatPlugin.Log.LogInfo($"Ignored a report from {player.Data.PlayerName} during the meeting transition.");
				return false;
			}

			if(targetPlayerId == byte.MaxValue && (GameManager.Instance == null || !GameManager.Instance.GameHasStarted || LobbyBehaviour.Instance != null))
			{
				// A report delayed across a round/lobby transition is ambiguous. Block it,
				// but do not use the confirmed-signature path.
				Anticheat.Flag(player, $"{player.Data.PlayerName} attempted to create an emergency meeting outside an active round.", false);
				return false;
			}

			if(targetPlayerId == byte.MaxValue && Anticheat.IsEmergencyMeetingDuringCooldown(out double secondsRemaining))
			{
				Anticheat.FlagConfirmedExploit(player, $"{player.Data.PlayerName} attempted an emergency meeting about {System.Math.Ceiling(secondsRemaining)} seconds before the configured cooldown expired.");
				return false;
			}

			// Official servers validate report authority. A lobby host's local body/death
			// snapshot can be behind the server, so accepting the result avoids dropping
			// legitimate reports without weakening genuinely host-authoritative lobbies.
			if(Utilities.IsAnticheatPresent())
			{
				Anticheat.GrantMeetingTransitionGrace(3);
				return true;
			}

			NetworkedPlayerInfo target = null;
			if(targetPlayerId != byte.MaxValue)
			{
				foreach(PlayerControl candidate in PlayerControl.AllPlayerControls)
				{
					if(candidate != null && candidate.Data != null && candidate.PlayerId == targetPlayerId)
					{
						target = candidate.Data;
						break;
					}
				}

				if(target == null)
				{
					Anticheat.Flag(player, $"Blocked a report for unknown player ID {targetPlayerId}.", false);
					return false;
				}
			}
			if(GameManager.Instance == null || !GameManager.Instance.GameHasStarted || LobbyBehaviour.Instance != null)
			{
				Anticheat.Flag(player, $"{player.Data.PlayerName} attempted to report outside an active round.");
				return false;
			}

			if(player.Data.IsDead || player.Data.Disconnected)
			{
				if(Anticheat.HasGhostRoleGrace(player)) return false;
				Anticheat.Flag(player, $"{player.Data.PlayerName} attempted to report while dead or disconnected.");
				return false;
			}

			if(GameManager.Instance.IsHideAndSeek())
			{
				Anticheat.Flag(player, $"{player.Data.PlayerName} attempted to call a meeting in Hide and Seek");
				return false;
			}

			// A null target is a legitimate emergency-button meeting. Body reports must identify a dead player.
			if(target != null && (!target.IsDead || target.Disconnected))
			{
				Anticheat.Flag(player, $"{player.Data.PlayerName} attempted to report a player who has no valid body.");
				return false;
			}

			Anticheat.GrantMeetingTransitionGrace(3);
			return true;
		}

		public override RpcCalls GetId()
		{
			return RpcCalls.ReportDeadBody;
		}
	}
}
