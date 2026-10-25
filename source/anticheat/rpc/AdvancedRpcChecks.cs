using AmongUs.GameOptions;
using Hazel;
using InnerNet;
using System;
using UnityEngine;

namespace AUAnticheat.anticheat.rpc
{
	internal static class AdvancedRuleHelpers
	{
		public static string Name(PlayerControl player)
		{
			return player != null && player.Data != null ? player.Data.PlayerName : "Unknown player";
		}

		public static bool IsActiveRound()
		{
			return GameManager.Instance != null && GameManager.Instance.GameHasStarted && ShipStatus.Instance != null;
		}

		public static bool IsRole(PlayerControl player, string roleName)
		{
			return player != null && player.Data != null && player.Data.RoleType.ToString() == roleName;
		}

		public static void FlagAuthority(PlayerControl objectOwner, string reason)
		{
			Anticheat.Flag(Anticheat.GetAuthoritativeActor(objectOwner), reason);
		}

		public static bool IsWithinAbilityDistance(PlayerControl source, PlayerControl target, float configuredDistance, float tolerance = 1.25f)
		{
			if(source == null || target == null) return false;
			return Vector2.Distance(source.GetTruePosition(), target.GetTruePosition()) <= configuredDistance + tolerance;
		}

		public static void GrantAllMovementGrace(double seconds = 5)
		{
			foreach(PlayerControl candidate in PlayerControl.AllPlayerControls)
			{
				Anticheat.GrantMovementGrace(candidate, seconds);
			}
		}
	}

	internal class SyncSettings : RpcCheck
	{
		public override bool Validate(PlayerControl player, MessageReader reader)
		{
			if(ShipStatus.Instance == null || Anticheat.IsRoundStartGrace()) return true;

			AdvancedRuleHelpers.FlagAuthority(player, $"The lobby host changed game settings after the round-start window.");
			return false;
		}

		public override RpcCalls GetId() => RpcCalls.SyncSettings;
		public override bool IsHostOnly() => true;
	}

	internal class MurderPlayer : RpcCheck
	{
		public override bool Validate(PlayerControl player, MessageReader reader)
		{
			PlayerControl target = reader.ReadNetObject<PlayerControl>();
			MurderResultFlags result = (MurderResultFlags)reader.ReadInt32();
			if((result & MurderResultFlags.Succeeded) == 0) return true;

			// This is the host-authorized result. Observer role/death/meeting snapshots
			// may already have advanced by the time it arrives, so they cannot be used
			// to accuse the host or suppress an ordinary kill. The defending host checks
			// the original CheckMurder request below.
			if(player == null || player.Data == null || target == null || target.Data == null || target == player)
			{
				Anticheat.Flag("Blocked a structurally invalid host-authorized murder result.");
				return false;
			}

			return true;
		}

		public override RpcCalls GetId() => RpcCalls.MurderPlayer;
		public override bool IsHostOnly() => true;
	}

	internal class StartMeeting : RpcCheck
	{
		public override bool Validate(PlayerControl player, MessageReader reader)
		{
			if(AdvancedRuleHelpers.IsActiveRound() && MeetingHud.Instance == null && !GameManager.Instance.IsHideAndSeek())
			{
				AdvancedRuleHelpers.GrantAllMovementGrace();
				return true;
			}

			PlayerControl actor = Anticheat.GetAuthoritativeActor(player);
			Anticheat.Flag(actor, $"{AdvancedRuleHelpers.Name(actor)} attempted to force a meeting in an invalid game state.");
			return false;
		}

		public override RpcCalls GetId() => RpcCalls.StartMeeting;
		public override bool IsHostOnly() => true;
	}

	internal abstract class MeetingAuthorityCheck : RpcCheck
	{
		public override bool Validate(PlayerControl player, MessageReader reader)
		{
			if(MeetingHud.Instance != null)
			{
				AdvancedRuleHelpers.GrantAllMovementGrace();
				return true;
			}

			PlayerControl host = Anticheat.GetHostPlayer();
			Anticheat.Flag(host, $"{AdvancedRuleHelpers.Name(host)} sent {GetId()} without an active meeting.");
			return false;
		}

		public override bool IsHostOnly() => true;
		public override Type GetExpectedNetObject() => typeof(MeetingHud);
	}

	internal class CloseMeeting : MeetingAuthorityCheck
	{
		public override RpcCalls GetId() => RpcCalls.CloseMeeting;
	}

	internal class VotingComplete : MeetingAuthorityCheck
	{
		public override RpcCalls GetId() => RpcCalls.VotingComplete;
	}

	internal class ClearVote : MeetingAuthorityCheck
	{
		public override RpcCalls GetId() => RpcCalls.ClearVote;
	}

	internal class BootFromVent : RpcCheck
	{
		public override bool Validate(PlayerControl player, MessageReader reader)
		{
			if(AdvancedRuleHelpers.IsActiveRound())
			{
				Anticheat.GrantMovementGrace(player);
				return true;
			}

			PlayerControl actor = Anticheat.GetAuthoritativeActor(player);
			Anticheat.Flag(actor, $"{AdvancedRuleHelpers.Name(actor)} attempted to boot a player from a vent outside active gameplay.");
			return false;
		}

		public override RpcCalls GetId() => RpcCalls.BootFromVent;
		public override bool IsHostOnly() => true;
		public override Type GetExpectedNetObject() => typeof(PlayerPhysics);
	}

	internal class SetRole : RpcCheck
	{
		public override bool Validate(PlayerControl player, MessageReader reader)
		{
			RoleTypes requestedRole = (RoleTypes)reader.ReadUInt16();
			reader.ReadBoolean();

			if(Utilities.IsAnticheatPresent() && RoleManager.IsGhostRole(requestedRole))
			{
				Anticheat.GrantGhostRoleGrace(player);
				Anticheat.GrantMovementGrace(player, 4);
				return true;
			}

			if(LobbyBehaviour.Instance == null && Anticheat.IsRoundStartGrace()) return true;
			if(LobbyBehaviour.Instance == null && player != null && !player.roleAssigned) return true;
			if(player != null && player.Data != null && player.Data.RoleType == requestedRole) return true;
			if(IsLegitimateGhostTransition(player, requestedRole)) return true;

			PlayerControl actor = Anticheat.GetAuthoritativeActor(player);
			string location = LobbyBehaviour.Instance != null ? "in the lobby" : "mid-game";
			Anticheat.Flag(actor, $"{AdvancedRuleHelpers.Name(actor)} changed {AdvancedRuleHelpers.Name(player)}'s role to {requestedRole} {location}.");
			return false;
		}

		public override RpcCalls GetId() => RpcCalls.SetRole;
		public override bool IsHostOnly() => true;

		private static bool IsLegitimateGhostTransition(PlayerControl player, RoleTypes requestedRole)
		{
			if(player == null || player.Data == null || !RoleManager.IsGhostRole(requestedRole)) return false;
			if(!player.Data.IsDead && !Anticheat.HasGhostRoleGrace(player)) return false;

			RoleTypes livingRole = player.Data.RoleWhenAlive.HasValue ? player.Data.RoleWhenAlive.Value : player.Data.RoleType;
			bool wasImpostor = RoleManager.IsImpostorRole(livingRole);
			return RoleManager.IsImpostorRole(requestedRole) == wasImpostor;
		}
	}

	internal class ProtectPlayer : RpcCheck
	{
		public override bool Validate(PlayerControl player, MessageReader reader)
		{
			PlayerControl target = reader.ReadNetObject<PlayerControl>();
			PlayerControl actor = Anticheat.GetAuthoritativeActor(player);
			if(AdvancedRuleHelpers.IsActiveRound() && AdvancedRuleHelpers.IsRole(player, "GuardianAngel") && target != null && target.Data != null) return true;

			Anticheat.Flag(actor, $"{AdvancedRuleHelpers.Name(actor)} issued an invalid Guardian Angel protection.");
			return false;
		}

		public override RpcCalls GetId() => RpcCalls.ProtectPlayer;
		public override bool IsHostOnly() => true;
	}

	internal class Shapeshift : RpcCheck
	{
		public override bool Validate(PlayerControl player, MessageReader reader)
		{
			PlayerControl target = reader.ReadNetObject<PlayerControl>();
			reader.ReadBoolean();
			PlayerControl actor = Anticheat.GetAuthoritativeActor(player);
			// Official servers have already validated this result. The local role snapshot
			// can arrive a few frames before or after the shapeshift RPC, especially for a
			// lagging client, so do not reinterpret a temporary duplicate appearance as a
			// cheat. The request-side CheckShapeshift validation remains active for hosts
			// that are the actual network authority.
			if(Utilities.IsAnticheatPresent() && AdvancedRuleHelpers.IsActiveRound() && player != null && target != null)
			{
				Anticheat.GrantMovementGrace(player, 3);
				return true;
			}

			// Host-authored revert/cleanup results may arrive after the death state.
			if(AdvancedRuleHelpers.IsActiveRound() && AdvancedRuleHelpers.IsRole(player, "Shapeshifter") && target != null)
			{
				Anticheat.GrantMovementGrace(player, 3);
				return true;
			}

			Anticheat.Flag(actor, $"{AdvancedRuleHelpers.Name(actor)} issued an invalid shapeshift for {AdvancedRuleHelpers.Name(player)}.");
			return false;
		}

		public override RpcCalls GetId() => RpcCalls.Shapeshift;
		public override bool IsHostOnly() => true;
	}

	internal class CheckMurder : RpcCheck
	{
		public override bool Validate(PlayerControl player, MessageReader reader)
		{
			PlayerControl target = reader.ReadNetObject<PlayerControl>();
			if(Utilities.IsAnticheatPresent()) return !Anticheat.ObserveDistinctTargetSpray(player, target, GetId());
			if(AdvancedRuleHelpers.IsActiveRound() && MeetingHud.Instance == null && player != null && player.Data != null && !player.Data.IsDead &&
				player.Data.Role != null && player.Data.Role.CanUseKillButton && target != null && target != player && target.Data != null && !target.Data.IsDead)
			{
				return !Anticheat.ObserveDistinctTargetSpray(player, target, GetId());
			}

			Anticheat.Flag(player, $"{AdvancedRuleHelpers.Name(player)} requested an impossible murder.");
			return false;
		}

		public override RpcCalls GetId() => RpcCalls.CheckMurder;
	}

	internal class CheckProtect : RpcCheck
	{
		public override bool Validate(PlayerControl player, MessageReader reader)
		{
			PlayerControl target = reader.ReadNetObject<PlayerControl>();
			if(Utilities.IsAnticheatPresent()) return !Anticheat.ObserveDistinctTargetSpray(player, target, GetId());
			if(AdvancedRuleHelpers.IsActiveRound() && AdvancedRuleHelpers.IsRole(player, "GuardianAngel") && target != null && target.Data != null && !target.Data.IsDead &&
				AdvancedRuleHelpers.IsWithinAbilityDistance(player, target, player.Data.Role.GetAbilityDistance(), 1.5f))
			{
				return !Anticheat.ObserveDistinctTargetSpray(player, target, GetId());
			}

			Anticheat.Flag(player, $"{AdvancedRuleHelpers.Name(player)} requested an impossible Guardian Angel protection.");
			return false;
		}

		public override RpcCalls GetId() => RpcCalls.CheckProtect;
	}

	internal class CheckShapeshift : RpcCheck
	{
		public override bool Validate(PlayerControl player, MessageReader reader)
		{
			PlayerControl target = reader.ReadNetObject<PlayerControl>();
			if(reader.BytesRemaining > 0) reader.ReadBoolean();
			if(Utilities.IsAnticheatPresent()) return !Anticheat.ObserveDistinctTargetSpray(player, target, GetId());
			if(AdvancedRuleHelpers.IsActiveRound() && AdvancedRuleHelpers.IsRole(player, "Shapeshifter") && !player.Data.IsDead && target != null)
			{
				return !Anticheat.ObserveDistinctTargetSpray(player, target, GetId());
			}

			Anticheat.Flag(player, $"{AdvancedRuleHelpers.Name(player)} requested an impossible shapeshift.");
			return false;
		}

		public override RpcCalls GetId() => RpcCalls.CheckShapeshift;
	}

	internal class TriggerSpores : RpcCheck
	{
		public override bool Validate(PlayerControl player, MessageReader reader)
		{
			if(Utilities.IsAnticheatPresent()) return true;
			if(AdvancedRuleHelpers.IsActiveRound() && Utilities.GetCurrentMap() == MapNames.Fungle && player != null && player.Data != null && !player.Data.IsDead) return true;

			Anticheat.Flag(player, $"{AdvancedRuleHelpers.Name(player)} triggered spores in an invalid state.");
			return false;
		}

		public override RpcCalls GetId() => RpcCalls.TriggerSpores;
	}

	internal abstract class PhantomCheck : RpcCheck
	{
		private readonly bool hostOnly;

		protected PhantomCheck(bool hostOnly)
		{
			this.hostOnly = hostOnly;
		}

		public override bool Validate(PlayerControl player, MessageReader reader)
		{
			if(Utilities.IsAnticheatPresent()) return true;
			bool validLiveRequest = !hostOnly && AdvancedRuleHelpers.IsRole(player, "Phantom") && player != null && player.Data != null && !player.Data.IsDead;
			bool validHostResult = hostOnly && (AdvancedRuleHelpers.IsRole(player, "Phantom") || Anticheat.HasGhostRoleGrace(player));
			if(AdvancedRuleHelpers.IsActiveRound() && (validLiveRequest || validHostResult)) return true;

			PlayerControl actor = hostOnly ? Anticheat.GetAuthoritativeActor(player) : player;
			Anticheat.Flag(actor, $"{AdvancedRuleHelpers.Name(actor)} used {GetId()} for a player who is not an active Phantom.");
			return false;
		}

		public override bool IsHostOnly() => hostOnly;
	}

	internal class CheckVanish : PhantomCheck
	{
		public CheckVanish() : base(false) { }
		public override RpcCalls GetId() => RpcCalls.CheckVanish;
	}

	internal class StartVanish : PhantomCheck
	{
		public StartVanish() : base(true) { }
		public override RpcCalls GetId() => RpcCalls.StartVanish;
	}

	internal class CheckAppear : PhantomCheck
	{
		public CheckAppear() : base(false) { }
		public override RpcCalls GetId() => RpcCalls.CheckAppear;
	}

	internal class StartAppear : PhantomCheck
	{
		public StartAppear() : base(true) { }
		public override RpcCalls GetId() => RpcCalls.StartAppear;
	}
}
