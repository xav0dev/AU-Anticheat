using System;
using System.Collections.Generic;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace AUAnticheat.anticheat
{
	internal static class ActivityTracker
	{
		private sealed class Activity { public float Joined; public uint? ObjectId; public int Messages; }
		private static readonly Dictionary<int, Activity> Players = new();
		private static string session = "";
		public static void Reset() { Players.Clear(); session = ""; }
		public static int Count { get { Ensure(); return Players.Count; } }
		private static void Ensure()
		{
			string current = AmongUsClient.Instance == null ? "offline" : $"{AmongUsClient.Instance.GameId}:{AmongUsClient.Instance.HostId}";
			if(current == session) return;
			session = current; Players.Clear();
		}
		public static void Forget(int id) { Ensure(); Players.Remove(id); }
		public static void Observe(int id, uint? netId, bool message)
		{
			Ensure();
			if(!Players.TryGetValue(id, out var state))
			{
				if(Players.Count >= 64) return;
				Players[id] = state = new Activity { Joined = Time.realtimeSinceStartup, ObjectId = netId };
			}
			if(netId.HasValue && state.ObjectId.HasValue && state.ObjectId != netId)
				Players[id] = state = new Activity { Joined = Time.realtimeSinceStartup, ObjectId = netId };
			if(netId.HasValue) state.ObjectId = netId;
			if(message) state.Messages = Math.Min(1000000, state.Messages + 1);
		}
		public static string Summary()
		{
			Ensure(); var rows = new List<string>();
			foreach(var pair in Players) rows.Add($"Lobby ID {pair.Key}: observed {Math.Max(0, (int)(Time.realtimeSinceStartup - pair.Value.Joined))}s, {pair.Value.Messages} messages affecting its objects.");
			return string.Join("\n", rows);
		}
		[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnPlayerJoined))]
		private static class OnJoin
		{
			private static void Postfix(ClientData __0)
			{
				if(__0 == null) return;
				Forget(__0.Id); Observe(__0.Id, null, false);
					HostLobbyAutomation.Joined(__0);
			}
		}
		public static void Tick()
		{
			Ensure();
			foreach(var player in PlayerControl.AllPlayerControls)
				if(player != null && player.Data != null && !player.Data.Disconnected) Observe(player.OwnerId, player.NetId, false);
		}
	}
}
