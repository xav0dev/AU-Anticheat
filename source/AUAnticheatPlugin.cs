using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using AUAnticheat.ui;
using UnityEngine;

namespace AUAnticheat;

[BepInPlugin("com.au.anticheat", "AU-Anticheat", "0.9.0.1")]
[BepInProcess("Among Us.exe")]
internal class AUAnticheatPlugin : BasePlugin
{
	internal const string DisplayVersion = "1.0.0-beta.1";
	internal static new ManualLogSource Log;
	private static readonly Harmony harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);

	private static MainUI mainUI;
	private static StatusIndicator statusIndicator;
	public static NotificationManager notifications;
	internal static bool ProtectionsAttached { get; private set; }

	public override void Load()
	{
		Log = base.Log;
		Log.LogInfo($"AU-Anticheat {DisplayVersion} (pre-release)");
		ProtectionsAttached = false;
		try
		{
			if(anticheat.ConfigMigration.ImportIfMissing(Paths.ConfigPath))
			{
				Config.Reload();
				Log.LogInfo("Imported previous plugin preferences into the AU-Anticheat configuration.");
			}
		}
		catch(System.Exception error)
		{
			Log.LogWarning($"Previous configuration could not be imported ({error.GetType().Name}). Original file retained; review lobby preferences.");
		}
		var exceptions = Config.Bind("Admission", "FriendCodeExceptions", "", "Semicolon-separated friend codes exempt only from the minimum-level rule (maximum 128).");
		anticheat.FriendCodeExceptions.Initialize(exceptions.Value, value => exceptions.Value = value);
		var autoRules = Config.Bind("LobbyRules", "Enabled", false, "Post saved rules once after a stable full waiting lobby (any supported capacity) reaches the chosen delay. Host only.");
		var rulesDelay = Config.Bind("LobbyRules", "FullLobbySeconds", 10, "Continuous full-lobby delay in seconds (1-120).");
		anticheat.LobbyRules.Initialize(System.IO.Path.Combine(Paths.ConfigPath, "AU-Anticheat"), autoRules.Value, rulesDelay.Value,
			(use, seconds) => { autoRules.Value = use; rulesDelay.Value = seconds; });

		mainUI = AddComponent<MainUI>();
		statusIndicator = AddComponent<StatusIndicator>();
		notifications = AddComponent<NotificationManager>();
		anticheat.SavedSettings.Initialize(System.IO.Path.Combine(Paths.ConfigPath, "AU-Anticheat"));
		anticheat.LobbyLoadouts.Initialize(System.IO.Path.Combine(Paths.ConfigPath, "AU-Anticheat"));
		anticheat.ModerationStore.Initialize(System.IO.Path.Combine(Paths.ConfigPath, "AU-Anticheat"));

		try
		{
			harmony.PatchAll();
			ProtectionsAttached = true;
		}
		catch(System.Exception exception)
		{
			anticheat.Anticheat.Enabled = false;
			Log.LogError($"Protection startup failed: {exception}");
			// PatchAll may have attached earlier hooks before a later hook failed.
			try { harmony.UnpatchSelf(); }
			catch(System.Exception cleanupError) { Log.LogError($"Partial-hook cleanup failed: {cleanupError}"); }
			notifications.Send("Startup Error", "Protections are inactive: a game hook failed to load. Check BepInEx/LogOutput.log and restart with a compatible build.", 9999);
			throw;
		}

		Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} has loaded!");
	}

	public static void Eject()
	{
		anticheat.SavedSettings.Save();
		ProtectionsAttached = false;
		harmony.UnpatchSelf();

		notifications.ClearNotifications();

		Object.Destroy(mainUI);
		Object.Destroy(statusIndicator);
		Object.Destroy(notifications);

		HideBuiltInModStamp();
	}

	private static void HideBuiltInModStamp()
	{
		if(ModManager.Instance != null && ModManager.Instance.ModStamp != null)
		{
			ModManager.Instance.ModStamp.enabled = false;
		}
	}

	[HarmonyPatch(typeof(ModManager), nameof(ModManager.ShowModStamp))]
	private static class HideModStamp
	{
		private static bool Prefix()
		{
			HideBuiltInModStamp();
			return false;
		}
	}

	[HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Awake))]
	class OnGameLoad
	{
		public static void Postfix()
		{
			anticheat.Anticheat.ResetSession();
			HideBuiltInModStamp();
			Log.LogInfo(ProtectionsAttached ? "AU-Anticheat protections are attached" : "AU-Anticheat protections are inactive");
		}
	}
}
