using Hazel;
using Il2CppInterop.Runtime;
using InnerNet;
using System;
using System.Collections.Generic;
using System.Linq;
using AUAnticheat.features;

namespace AUAnticheat.anticheat.rpc
{
	internal class UpdateSystem : RpcCheck
	{
		// TODO: Maybe change the variable name to something shorter lol?
		private static readonly SystemTypes[] SystemsThatCanBeUpdatedWhenDead = {
			SystemTypes.MedBay,
			SystemTypes.Sabotage,
			// Ghosts update the Security system when closing cameras, but not opening them
			SystemTypes.Security,
			SystemTypes.Ventilation
		};

		private static readonly Dictionary<Il2CppSystem.Type, Func<PlayerControl, MessageReader, bool>> systemHandlers = new Dictionary<Il2CppSystem.Type, Func<PlayerControl, MessageReader, bool>>()
		{
			{ Il2CppType.From(typeof(SwitchSystem)), ValidateSwitchSystem },
			{ Il2CppType.From(typeof(MushroomMixupSabotageSystem)), ValidateMushroomMixupSystem },
			{ Il2CppType.From(typeof(SabotageSystemType)), ValidateSabotageSystem }
		};

		public override bool Validate(PlayerControl player, MessageReader reader)
		{
			SystemTypes system = (SystemTypes)reader.ReadByte();
			player = reader.ReadNetObject<PlayerControl>();
			if(ShipStatus.Instance == null || player == null || player.Data == null)
			{
				Anticheat.Flag("Blocked a system update with an unavailable map/player. No sender attribution.");
				return false;
			}

			ShipStatus.Instance.Systems.TryGetValue(system, out ISystemType systemInterface);
			if(systemInterface == null)
			{
				Anticheat.Flag(player, $"{player.Data.PlayerName} tried to update system {system} when the current map has no such system.");
				return false;
			}

			if(system == SystemTypes.Ventilation && IsSuspiciousVentilationSequence(player, reader))
			{
				return false;
			}

			if(system == SystemTypes.Sabotage && ObserveSabotageTargetSweep(player, reader))
			{
				return false;
			}

			// On non-host clients this is already a host-relayed result. The embedded
			// player identifies the original actor, not the network sender, and local
			// state may be one update ahead or behind. Only the defending host performs
			// role/state/operation enforcement.
			if(AmongUsClient.Instance != null && (!AmongUsClient.Instance.AmHost || Utilities.IsAnticheatPresent())) return true;

			if(player.Data.IsDead && !SystemsThatCanBeUpdatedWhenDead.Contains(system))
			{
				Anticheat.Flag(player, $"{player.Data.PlayerName} tried to update system {system} while dead.");
				return false;
			}

			// thanks https://discord.com/channels/623153565053222947/754333645199900723/1292612730418757785
			Il2CppSystem.Type systemType = Il2CppType.TypeFromPointer(systemInterface.ObjectClass);

			systemHandlers.TryGetValue(systemType, out Func<PlayerControl, MessageReader, bool> handler);
			if(handler == null) return true;

			return handler(player, reader);
		}

		private static bool ObserveSabotageTargetSweep(PlayerControl player, MessageReader reader)
		{
			int oldPosition = reader.Position;
			try
			{
				if(reader.BytesRemaining < 1) return false;
				SystemTypes target = (SystemTypes)reader.ReadByte();
				if(!Utilities.IsValidSabotage(target)) return false;
				return Anticheat.ObserveSabotageSweep(player, target);
			}
			finally
			{
				reader.Position = oldPosition;
			}
		}

		private static bool IsSuspiciousVentilationSequence(PlayerControl player, MessageReader reader)
		{
			int oldPosition = reader.Position;
			try
			{
				if(reader.BytesRemaining < 4)
				{
					PacketPreflight.Block("truncated ventilation sequence/operation/vent payload");
					return true;
				}
				byte[] header = { reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte() };
				if(!VentilationRules.TryRead(header, out _, out byte op, out byte ventId))
				{
					PacketPreflight.Block("unknown ventilation operation");
					return true;
				}
				VentilationSystem.Operation operation = (VentilationSystem.Operation)op;
				if(!VentRuleHelpers.ValidateLoadedVent(ventId)) return true;
				if(VentilationRules.IsMovement(op))
				{
					// Newer builds move through this system instead of legacy vent RPCs.
					// Replicated inVent/position flags can arrive after the operation.
					Anticheat.GrantMovementGrace(player);
					return false;
				}
				if(!Protections.ProtectAgainstNonHostKickExploit) return false;
				if(operation == VentilationSystem.Operation.StartCleaning)
				{
					if(HasIncompleteVentCleaningTask(player))
					{
						Anticheat.GrantVentCleaningGrace(player, ventId);
						return false;
					}

					FlagSuspiciousVentilation(player, operation, ventId);
					return true;
				}

				if(operation == VentilationSystem.Operation.BootImpostors)
				{
					// BootImpostors is also emitted by the legitimate Vent Cleaning task.
					// It is accepted exactly once and only after the matching, task-backed
					// StartCleaning operation. Merely owning the task is not sufficient:
					// exploit menus can forge the embedded player and send BootImpostors alone.
					if(Anticheat.TryConsumeVentCleaningGrace(player, ventId)) return false;

					FlagSuspiciousVentilation(player, operation, ventId);
					return true;
				}

				return false;
			}
			finally
			{
				reader.Position = oldPosition;
			}
		}

		private static bool HasIncompleteVentCleaningTask(PlayerControl player)
		{
			if(player == null || player.Data == null || player.Data.Tasks == null) return false;

			foreach(NetworkedPlayerInfo.TaskInfo task in player.Data.Tasks)
			{
				if(task.TypeId == (byte)TaskTypes.VentCleaning && !task.Complete) return true;
			}

			return false;
		}

		private static void FlagSuspiciousVentilation(PlayerControl player, VentilationSystem.Operation operation, ushort ventId)
		{
			string name = player != null && player.Data != null ? player.Data.PlayerName : "Unknown player";
			Anticheat.FlagConfirmedExploit(player, $"{name} sent an unexplained ventilation {operation} operation for vent {ventId}.");
		}

		// The Mushroom Mixup system is only updated in the SabotageSystemType::Update function by the host. It should never be sent by a player
		private static bool ValidateMushroomMixupSystem(PlayerControl player, MessageReader reader)
		{
			MushroomMixupSabotageSystem.Operation operation = (MushroomMixupSabotageSystem.Operation)reader.ReadByte();
			if(AmongUsClient.Instance != null && !AmongUsClient.Instance.AmHost)
			{
				// Observers see the result after host processing; the embedded player can
				// still be the original sabotager, so it is not sender attribution.
				return true;
			}

			Anticheat.Flag(player, $"{player.Data.PlayerName} attempted to update Mushroom Mixup system with operation {operation}.");
			return false;
		}

		private static bool ValidateSabotageSystem(PlayerControl player, MessageReader reader)
		{
			SystemTypes system = (SystemTypes)reader.ReadByte();

			if(!Utilities.IsValidSabotage(system))
			{
				Anticheat.Flag(player, $"{player.Data.PlayerName} attempted to sabotage an invalid system: {system}.");
				return false;
			}

			if(!RoleManager.IsImpostorRole(player.Data.RoleType))
			{
				Anticheat.Flag(player, $"{player.Data.PlayerName} attempted to sabotage {system} when they are not an imposter.");
				return false;
			}

			if(GameManager.Instance.IsHideAndSeek())
			{
				Anticheat.Flag(player, $"{player.Data.PlayerName} attempted to sabotage {system} while in Hide and Seek.");
				return false;
			}

			return true;
		}

		private static bool ValidateSwitchSystem(PlayerControl player, MessageReader reader)
		{
			byte switches = reader.ReadByte();

			if(switches.HasBit(128))
			{
				Anticheat.Flag(player, $"{player.Data.PlayerName} attempted to bulk-update switches: {switches}.");
				return false;
			}

			if(switches > 5)
			{
				Anticheat.Flag(player, $"{player.Data.PlayerName} attempted to toggle an invalid switch: {switches}.");
				return false;
			}

			// Block light switch updates when lights are currently not sabotaged
			// It is possible for this check to false flag if a player is attempting to fix lights when they have not received the message about the sabotage being fixed
			// This is also why you may experience the bug where lights get unfixed right after they get fixed
			// So to avoid wrongly banning players, we just silent flag and block the RPC to prevent hackers from being able to force sabotage lights
			SwitchSystem system = ShipStatus.Instance.Systems[SystemTypes.Electrical].Cast<SwitchSystem>();
			if(system.ExpectedSwitches == system.ActualSwitches)
			{
				AUAnticheatPlugin.Log.LogInfo($"Blocked switch update from {player.Data.PlayerName} as lights are not currently sabotaged");
				return false;
			}

			// False positives may be possible if a player is toggling light switches before their client receives the StartMeeting RPC so we silent flag
			// Maybe we can check to see what state the meeting is in, and if it is after the meeting was animated then flag the player?
			if(MeetingHud.Instance)
			{
				AUAnticheatPlugin.Log.LogInfo($"Blocked switch update from {player.Data.PlayerName} as there is a currently active meeting");
				return false;
			}

			return true;
		}

		public override RpcCalls GetId()
		{
			return RpcCalls.UpdateSystem;
		}

		public override Type GetExpectedNetObject()
		{
			return typeof(ShipStatus);
		}
	}
}
