using Hazel;

namespace AUAnticheat.anticheat.rpc
{
	internal class PlayAnimation : RpcCheck
	{
		public override bool Validate(PlayerControl player, MessageReader reader)
		{
			TaskTypes animation = (TaskTypes)reader.ReadByte();
			if(Utilities.IsAnticheatPresent()) return true;

			if(LobbyBehaviour.Instance)
			{
				Anticheat.Flag(player, $"{player.Data.PlayerName} sent the PlayAnimation RPC for task {animation} inside the lobby.", false);
				return false;
			}

			if(RoleManager.IsImpostorRole(player.Data.RoleType))
			{
				Anticheat.Flag(player, $"{player.Data.PlayerName} sent the PlayAnimation RPC for task {animation} when they are an imposter.", false);
				return false;
			}

			if(!GameManager.Instance.LogicOptions.GetVisualTasks())
			{
				Anticheat.Flag(player, $"{player.Data.PlayerName} sent the PlayAnimation RPC for task {animation} when visual tasks are off.", false);
				return false;
			}

			return true;
		}

		public override RpcCalls GetId()
		{
			return RpcCalls.PlayAnimation;
		}
	}
}
