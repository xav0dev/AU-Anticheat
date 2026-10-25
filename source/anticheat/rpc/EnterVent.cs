using Hazel;
using System;
using UnityEngine;

namespace AUAnticheat.anticheat.rpc
{
	internal class EnterVent : RpcCheck
	{
		public override bool Validate(PlayerControl player, MessageReader reader)
		{
			int ventId = reader.ReadPackedInt32();
			if(!VentRuleHelpers.ValidateLoadedVent(ventId)) return false;
			bool initialized = ShipStatus.Instance != null && player != null && player.Data != null && player.Data.Role != null;
			if(!initialized) return true;
			bool authority = AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost && !Utilities.IsAnticheatPresent();
			bool transition = Anticheat.IsRoundStartGrace() || Anticheat.HasMeetingTransitionGrace() ||
				Anticheat.HasGhostRoleGrace(player) || Anticheat.HasMovementGrace(player) ||
				(player.MyPhysics != null && player.MyPhysics.DoingCustomAnimation);
			if(!VentilationRules.ShouldCheckEntryRole(authority, initialized, transition, player.Data.IsDead))
			{
				Anticheat.GrantMovementGrace(player);
				return true;
			}
			if(!VentilationRules.RoleAllowsEntry(player.Data.Role.CanVent, player.Data.RoleType == AmongUs.GameOptions.RoleTypes.Engineer))
			{
				return PacketPreflight.Block("vent entry inconsistent with current role; affected object is not sender proof");
			}
			// Arrival position is not request position; lagged distance is not proof.
			if(GameManager.Instance != null && GameManager.Instance.IsHideAndSeek() && RoleManager.IsImpostorRole(player.Data.RoleType))
			{
				return PacketPreflight.Block("seeker vent entry disallowed in current game mode");
			}

			Anticheat.GrantMovementGrace(player);
			return true;
		}

		public override RpcCalls GetId()
		{
			return RpcCalls.EnterVent;
		}

		public override Type GetExpectedNetObject()
		{
			return typeof(PlayerPhysics);
		}
	}

	internal static class VentRuleHelpers
	{
		public const float MaximumEntryDistance = 5.0f;

		public static bool ValidateLoadedVent(int ventId)
		{
			if(ShipStatus.Instance == null || ShipStatus.Instance.AllVents == null || ShipStatus.Instance.AllVents.Length == 0 || Anticheat.IsRoundStartGrace()) return true;
			if(FindVent(ventId) != null) return true;
			return PacketPreflight.Block("vent identifier absent from loaded map");
		}

		public static Vent FindVent(int ventId)
		{
			if(ShipStatus.Instance == null || ShipStatus.Instance.AllVents == null) return null;
			foreach(Vent vent in ShipStatus.Instance.AllVents)
			{
				if(vent != null && vent.Id == ventId) return vent;
			}
			return null;
		}
	}
}
