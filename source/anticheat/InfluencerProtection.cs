using HarmonyLib;
using Hazel;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace AUAnticheat.anticheat
{
	internal static class InfluencerProtection
	{
		public static bool Enabled = true;
		private static readonly InfluencerBurstWindow Window = new();
		private static int feedId;
		public static void Reset() { Window.Reset(); feedId = 0; }

		[HarmonyPatch(typeof(SocialMediumFeedSystem), nameof(SocialMediumFeedSystem.HandleSpiritGuideMessage))]
		private static class BeforeInfluencerDecode
		{
			[HarmonyPriority(Priority.First)]
			private static bool Prefix(MessageReader __0)
			{
				// Even direct entry must be checked BEFORE ReadBytesAndSize allocates.
				return !Enabled || PacketPreflight.ValidateRpc(RpcCalls.SpiritGuideMessage, __0);
			}
		}

		[HarmonyPatch(typeof(SocialMediumFeedSystem), nameof(SocialMediumFeedSystem.AddImageToFeed))]
		private static class BeforeInfluencerImages
		{
			[HarmonyPriority(Priority.First)]
			private static bool Prefix(SocialMediumFeedSystem __instance, Il2CppStructArray<byte> __0)
			{
				if(!Enabled) return true;
				if(__0 == null || !InfluencerRules.ValidImageCount(__0.Length))
					return PacketPreflight.Block("Influencer message must contain 1 to 3 images");
				// Use the shipped catalog, not a guessed asset count. This shared RPC
				// provides no trustworthy sender identity to punish.
				var sprites = __instance.PossibleSpriteList;
				if(sprites == null || sprites.Count == 0) return PacketPreflight.Block("Influencer image catalog is not ready");
				for(int i = 0; i < __0.Length; i++)
					if(!InfluencerRules.ValidImageIndex(__0[i], sprites.Count))
						return PacketPreflight.Block("Influencer image index is outside the current catalog");
				int currentId = __instance.GetInstanceID();
				if(feedId != currentId) { Window.Reset(); feedId = currentId; }
				if(!Window.TryAccept(Time.realtimeSinceStartup))
					return PacketPreflight.Block("Influencer image feed resource limit reached (not a player conviction)");
				return true;
			}
		}
	}
}
