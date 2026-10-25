using Hazel;

namespace AUAnticheat.anticheat.rpc
{
	internal class SetScanner : RpcCheck
	{
		private readonly ScannerReplayWindow replays = new();
		private string session = "";
		public override bool Validate(PlayerControl player, MessageReader reader)
		{
			bool scanning = reader.ReadBoolean();
			byte sequence = reader.ReadByte();
			string key = $"{AmongUsClient.Instance?.GameId}:{AmongUsClient.Instance?.HostId}:{ShipStatus.Instance?.GetInstanceID()}";
			if(key != session) { replays.Reset(); session=key; }
			if(player != null && !replays.Accept(player.NetId,scanning,sequence,UnityEngine.Time.realtimeSinceStartup))
				return PacketPreflight.Block("repeated scan-start animation sequence; duplicate suppressed, not proof of a cheat menu");
			if(Utilities.IsAnticheatPresent()) return true;

			// The Medbay scan task can only be done if the map itself exists
			// If the game has not started yet (which the vanilla anticheat should already check), or if we're in the lobby to actual game transition, or the map was despawned,
			// then we know that SetScanner RPC was sent illegitimately
			if(ShipStatus.Instance == null && scanning)
			{
				Anticheat.Flag(player, $"{player.Data.PlayerName} sent the SetScanner RPC while the map has not spawned in yet.", false);
				return false;
			}

			// When a player gets killed, a SetScanner RPC with the scanning value sent to false is sent
			// This applies to Imposters too if they were to somehow die, so we need to account for this false flag
			if(RoleManager.IsImpostorRole(player.Data.RoleType) && scanning)
			{
				Anticheat.Flag(player, $"{player.Data.PlayerName} sent the SetScanner RPC when they are an imposter.", false);
				return false;
			}

			if(scanning && (GameManager.Instance == null || !GameManager.Instance.LogicOptions.GetVisualTasks()))
			{
				Anticheat.Flag(player, $"{player.Data.PlayerName} sent the SetScanner RPC while visual tasks were disabled.", false);
				return false;
			}

			// Do not infer task ownership from the replicated task list. Task assignment
			// and SetScanner can be observed in different orders, and scanner animation
			// itself grants no gameplay advantage. Structural, role, visual-setting, and
			// flood validation remain active.

			return true;
		}

		public override RpcCalls GetId()
		{
			return RpcCalls.SetScanner;
		}
	}
}
