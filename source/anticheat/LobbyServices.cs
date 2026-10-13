using System;
using UnityEngine;

namespace AUAnticheat.anticheat
{
	internal static class LobbyServices
	{
		private static float nextTick;
		private static bool activityFailed, extensionFailed, reportsFailed, chatFailed, rulesFailed, boundaryFailed, idleFailed;
		public static void Tick()
		{
			if(!AUAnticheatPlugin.ProtectionsAttached || Time.realtimeSinceStartup < nextTick) return;
			nextTick = Time.realtimeSinceStartup + 0.1f;
			Run(ActivityTracker.Tick, ref activityFailed, "Activity tracking");
			Run(LobbyIdleMonitor.Tick, ref idleFailed, "Lobby idle alerts");
			Run(SceneBoundaryMonitor.Tick, ref boundaryFailed, "Scene boundary observations");
			Run(LobbyExtension.Tick, ref extensionFailed, "Lobby extension");
			Run(RoundReports.Tick, ref reportsFailed, "Match reports");
			Run(LobbyRules.Tick, ref rulesFailed, "Lobby rules");
			Run(LobbyChat.Tick, ref chatFailed, "Shared chat alerts");
		}
		private static void Run(Action action, ref bool failed, string name)
		{
			if(failed) return;
			try { action(); }
			catch(Exception e)
			{
				failed = true;
				AUAnticheatPlugin.Log.LogWarning($"{name} disabled: {e}");
				AUAnticheatPlugin.notifications.Send(name, "This feature stopped after an error. Other protections remain active. Please check the log.");
			}
		}
	}
}
