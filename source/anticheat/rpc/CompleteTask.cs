using Hazel;

namespace AUAnticheat.anticheat.rpc
{
	internal class CompleteTask : RpcCheck
	{
		public override bool Validate(PlayerControl player, MessageReader reader)
		{
			uint taskIndex = reader.ReadPackedUInt32();
			if(player == null || player.Data == null)
			{
				Anticheat.Flag("Blocked CompleteTask without a valid player object.");
				return false;
			}

			// Arrival bursts are observational: lag can batch real completions.
			if(Anticheat.ObserveTaskCompletionSweep(player, taskIndex)) return false;

			// The official service is authoritative and may deliver the completion before
			// this client's role/task snapshot catches up.
			if(Utilities.IsAnticheatPresent()) return true;

			// If there is no instance of ShipStatus (such as if the game has not started yet or the map was despawned), then it is not possible to complete tasks (
			// Technically we don't need this to detect if someone completes a task in the lobby, as the task ID being greater than the total amount of tasks check should detect it
			if(ShipStatus.Instance == null)
			{
				Anticheat.Flag(player, $"{player.Data.PlayerName} tried completing task {taskIndex} when there was no valid instance of ShipStatus.");
				return false;
			}

			if(RoleManager.IsImpostorRole(player.Data.RoleType))
			{
				Anticheat.Flag(player, $"{player.Data.PlayerName} tried completing task {taskIndex} while being an imposter.");
				return false;
			}

			// Task IDs are zero-indexed
			if(player.Data.Tasks == null) return true;
			if(taskIndex >= (uint)player.Data.Tasks.Count)
			{
				Anticheat.Flag(player, $"{player.Data.PlayerName} tried completing task {taskIndex} when they only have {player.Data.Tasks.Count} tasks.");
				return false;
			}

			if(player.Data.Tasks[(int)taskIndex].Complete)
			{
				// Completion is idempotent, so a duplicate delivery cannot grant credit.
				AUAnticheatPlugin.Log.LogInfo($"Ignored duplicate task completion {taskIndex} from {player.Data.PlayerName}.");
				return false;
			}

			return true;
		}

		public override RpcCalls GetId()
		{
			return RpcCalls.CompleteTask;
		}
	}
}
