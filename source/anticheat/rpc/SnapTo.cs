using Hazel;
using System;
using UnityEngine;

namespace AUAnticheat.anticheat.rpc
{
	internal class SnapTo : RpcCheck
	{
		public override bool Validate(PlayerControl player, MessageReader reader)
		{
			Vector2 position = NetHelpers.ReadVector2(reader);
			reader.ReadUInt16();

			// Lobby spawn/position corrections legitimately use SnapTo. Only active-round jumps are judged.
			if(LobbyBehaviour.Instance != null)
			{
				Anticheat.GrantMovementGrace(player, 1);
				return true;
			}

			// The official server is the movement authority and sends legitimate map,
			// decontamination, role and correction snaps through remote-owned objects.
			if(Utilities.IsAnticheatPresent())
			{
				Anticheat.GrantMovementGrace(player, 2);
				return true;
			}

			// Ghosts (including Influencer), vent travel and transitions are not
			// living-player teleports. Observers cannot override host state.
			if(AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost && MovementContext.StableLivingPlayer(player))
			{
				float distance = Vector2.Distance(player.transform.position, position);
				if(distance > 8.0f)
				{
					// A forged target ID may belong to a victim. Block before applying
					// the change without awarding points to the affected player.
					return PacketPreflight.Block($"unexpected {distance:F1}-unit living-player teleport");
				}
			}

			Anticheat.GrantMovementGrace(player, 1);

			return true;
		}

		public override RpcCalls GetId()
		{
			return RpcCalls.SnapTo;
		}

		public override Type GetExpectedNetObject()
		{
			return typeof(CustomNetworkTransform);
		}
	}
}
