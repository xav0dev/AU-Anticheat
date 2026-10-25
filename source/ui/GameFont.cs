using System;
using TMPro;
using UnityEngine;

namespace AUAnticheat.ui
{
	internal static class GameFont
	{
		private static Font cached;
		private static float nextLookup;
		public static Font Current
		{
			get
			{
				if(cached != null || Time.realtimeSinceStartup < nextLookup) return cached;
				nextLookup = Time.realtimeSinceStartup + 5;
				try
				{
					// Reuse installed game assets; do not redistribute proprietary font files.
					cached = TMP_Settings.defaultFontAsset?.sourceFontFile;
					if(cached != null) return cached;
					foreach(var asset in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
					{
						if(asset == null || asset.sourceFontFile == null) continue;
						string name = asset.name ?? "";
						if(name.IndexOf("VCR",StringComparison.OrdinalIgnoreCase)>=0 || name.IndexOf("Barlow",StringComparison.OrdinalIgnoreCase)>=0)
						{ cached = asset.sourceFontFile; break; }
					}
				}
				catch(Exception) { /* Font lookup is cosmetic; never disable the menu. */ }
				return cached;
			}
		}
	}
}
