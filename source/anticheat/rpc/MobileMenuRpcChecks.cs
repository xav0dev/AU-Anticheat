using Hazel;
using System;

namespace AUAnticheat.anticheat.rpc
{
	internal sealed class CheckColor : RpcCheck
	{
		public override bool Validate(PlayerControl player, MessageReader reader)
		{
			byte color = reader.ReadByte();
			if(color >= Palette.ColorNames.Length)
			{
				Anticheat.Flag(player, $"{SafeName(player)} requested invalid color {color}.");
				return false;
			}

			if(GameManager.Instance != null && GameManager.Instance.GameHasStarted && LobbyBehaviour.Instance == null)
			{
				Anticheat.Flag(player, $"{SafeName(player)} attempted to change color after the match started.");
				return false;
			}

			if(Anticheat.ObserveRapidColorCycling(player, color)) return false;

			return true;
		}

		public override RpcCalls GetId() => RpcCalls.CheckColor;

		private static string SafeName(PlayerControl player)
		{
			return player != null && player.Data != null ? player.Data.PlayerName : "A player";
		}
	}

	internal sealed class CosmeticChange : RpcCheck
	{
		private readonly RpcCalls id;
		private readonly string cosmeticName;

		public CosmeticChange(RpcCalls id, string cosmeticName)
		{
			this.id = id;
			this.cosmeticName = cosmeticName;
		}

		public override bool Validate(PlayerControl player, MessageReader reader)
		{
			string cosmeticId = reader.ReadString();
			if(cosmeticId == null || cosmeticId.Length > 128 || ContainsControlCharacters(cosmeticId))
			{
				Anticheat.Flag(player, $"{SafeName(player)} sent a malformed {cosmeticName} identifier.");
				return false;
			}

			if(GameManager.Instance != null && GameManager.Instance.GameHasStarted && LobbyBehaviour.Instance == null)
			{
				Anticheat.Flag(player, $"{SafeName(player)} attempted a mid-game {cosmeticName} change.");
				return false;
			}

			return true;
		}

		public override RpcCalls GetId() => id;

		private static bool ContainsControlCharacters(string value)
		{
			foreach(char character in value)
			{
				if(char.IsControl(character)) return true;
			}
			return false;
		}

		private static string SafeName(PlayerControl player)
		{
			return player != null && player.Data != null ? player.Data.PlayerName : "A player";
		}
	}

	internal sealed class SendChat : RpcCheck
	{
		private const int MaximumChatLength = 400;

		public override bool Validate(PlayerControl player, MessageReader reader)
		{
			string message = reader.ReadString();
			if(message == null || message.Length > MaximumChatLength)
			{
				Anticheat.Flag(player, $"Blocked an oversized chat message attributed to {SafeName(player)}.", false);
				return false;
			}

			foreach(char character in message)
			{
				if(character == '\0' || (char.IsControl(character) && character != '\n'))
				{
					Anticheat.Flag(player, $"Blocked a chat message with unsafe control characters attributed to {SafeName(player)}.", false);
					return false;
				}
			}

			return true;
		}

		public override RpcCalls GetId() => RpcCalls.SendChat;

		private static string SafeName(PlayerControl player)
		{
			return player != null && player.Data != null ? player.Data.PlayerName : "an unknown player";
		}
	}

	internal sealed class CastVoteCheck : RpcCheck
	{
		private const byte SkipVoteId = 253;

		public override bool Validate(PlayerControl player, MessageReader reader)
		{
			byte sourcePlayerId = reader.ReadByte();
			byte suspectPlayerId = reader.ReadByte();
			PlayerControl claimedSource = FindPlayer(sourcePlayerId);

			if(MeetingHud.Instance == null)
			{
				if(Anticheat.HasMeetingTransitionGrace()) return false;
				Anticheat.Flag($"Blocked a vote outside a meeting claiming player id {sourcePlayerId}.");
				return false;
			}

			if(Utilities.IsAnticheatPresent()) return true;

			if(claimedSource == null || claimedSource.Data == null || claimedSource.Data.IsDead || claimedSource.Data.Disconnected)
			{
				Anticheat.Flag($"Blocked a vote with an invalid or inactive claimed voter ({sourcePlayerId}).");
				return false;
			}

			if(suspectPlayerId != SkipVoteId && FindPlayer(suspectPlayerId) == null)
			{
				Anticheat.Flag($"Blocked a vote for invalid player id {suspectPlayerId}.");
				return false;
			}

			if(AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost && MeetingHud.Instance.DidVote(sourcePlayerId))
			{
				AUAnticheatPlugin.Log.LogInfo($"Ignored a duplicate vote claiming to be from {claimedSource.Data.PlayerName}.");
				return false;
			}

			return true;
		}

		public override RpcCalls GetId() => RpcCalls.CastVote;

		public override Type GetExpectedNetObject() => typeof(MeetingHud);

		private static PlayerControl FindPlayer(byte playerId)
		{
			foreach(PlayerControl candidate in PlayerControl.AllPlayerControls)
			{
				if(candidate != null && candidate.PlayerId == playerId) return candidate;
			}
			return null;
		}
	}
}
