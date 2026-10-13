using UnityEngine;

namespace AUAnticheat.ui
{
	internal static class PlayerPresentation
	{
		public static string Name(PlayerControl player) => anticheat.PublicText.Name(player?.Data?.PlayerName);
		public static Color GetColor(PlayerControl player)
		{
			var outfit = player?.CurrentOutfit ?? player?.Data?.DefaultOutfit;
			int id = outfit == null ? -1 : outfit.ColorId;
			return Palette.PlayerColors != null && id >= 0 && id < Palette.PlayerColors.Length
				? (Color)Palette.PlayerColors[id] : Color.white;
		}
		public static GUIStyle NameStyle(PlayerControl player, bool selected)
		{
			return Styles.PlayerName(GetColor(player),selected);
		}
		public static string Details(PlayerControl player)
		{
			if(player?.Data == null) return "Player details are loading.";
			var peer = AmongUsClient.Instance?.FindClientById(player.OwnerId);
			// Never borrow an old connection's details for a replacement character.
			if(peer != null && (peer.Character != player || peer.IsBeingCreated)) peer = null;
			string code = anticheat.PublicText.Clean(peer?.FriendCode ?? player.Data.FriendCode,36);
			string account = anticheat.PublicText.Clean(peer?.ProductUserId,64);
			// Peer and player-info updates are not atomic. A display mismatch must
			// not hide both values forever; admission uses separate, stricter checks.
			uint reported = player.Data.PlayerLevel != uint.MaxValue ? player.Data.PlayerLevel : peer?.PlayerLevel ?? uint.MaxValue;
			string level = reported == uint.MaxValue ? "Unavailable (not received)" : ((ulong)reported+1).ToString();
			if(reported == 0) level += " (unconfirmed: may still be initializing)";
			if(player.Data.PlayerLevel != uint.MaxValue && peer != null && peer.PlayerLevel != uint.MaxValue && peer.PlayerLevel != player.Data.PlayerLevel)
				level += $" (peer reports {(ulong)peer.PlayerLevel+1}; records differ)";
			string platform = peer?.PlatformData == null ? "Unavailable" : peer.PlatformData.Platform.ToString() switch
			{
				"StandaloneEpicPC"=>"PC / Epic", "StandaloneSteamPC"=>"PC / Steam", "StandaloneMac"=>"Mac",
				"StandaloneWin10"=>"PC / Microsoft", "StandaloneItch"=>"PC / itch.io", "IPhone"=>"iOS",
				"Android"=>"Android", "Switch"=>"Nintendo Switch", "Xbox"=>"Xbox", "Playstation"=>"PlayStation", _=>"Unknown"
			};
			return $"Friend code: {(code.Length==0?"Unavailable":code)}\nAccount ID: {(account.Length==0?"Unavailable":account)}\nLevel: {level}     Platform: {platform}";
		}
	}
}
