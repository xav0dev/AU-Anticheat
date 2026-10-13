using AmongUs.InnerNet.GameDataMessages;
using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;
using AUAnticheat.anticheat.gamedata;
using AUAnticheat.anticheat.rpc;
using InnerNet;
using System;
using System.Collections.Generic;

namespace AUAnticheat.anticheat
{
	internal static class Anticheat
	{
		private sealed class ViolationState
		{
			public int Score;
			public int Count;
			public DateTime LastSeenUtc;
			public DateTime LastBurstAlertUtc;
			public DateTime MovementGraceUntilUtc;
			public DateTime GhostRoleGraceUntilUtc;
			public DateTime VentCleaningGraceUntilUtc;
			public int VentCleaningVentId = -1;
			public bool Punished;
			public bool Shared;
			public readonly Queue<DateTime> RecentMessages = new Queue<DateTime>();
			public readonly Dictionary<RpcCalls, Queue<DateTime>> MessagesByType = new Dictionary<RpcCalls, Queue<DateTime>>();
			public readonly Dictionary<string, Queue<DateTime>> MessagesByCategory = new Dictionary<string, Queue<DateTime>>();
			public readonly Dictionary<RpcCalls, Queue<TargetedAction>> TargetedActionsByType = new Dictionary<RpcCalls, Queue<TargetedAction>>();
			public readonly Queue<ColorRequest> RecentColorRequests = new Queue<ColorRequest>();
			public readonly Queue<IndexedAction> RecentTaskCompletions = new Queue<IndexedAction>();
			public readonly Queue<IndexedAction> RecentSabotageRequests = new Queue<IndexedAction>();
		}

		private sealed class TargetedAction
		{
			public byte TargetPlayerId;
			public DateTime TimeUtc;
		}

		private sealed class ColorRequest
		{
			public byte Color;
			public DateTime TimeUtc;
		}

		private sealed class IndexedAction
		{
			public int Value;
			public DateTime TimeUtc;
		}

		private static readonly Dictionary<int, ViolationState> Violations = new Dictionary<int, ViolationState>();
		private static readonly List<string> Incidents = new List<string>();
		// Shared-object payloads contain a claimed actor, not an authenticated sender.
		[ThreadStatic] private static int unattributedValidationDepth;
		private static string sessionKey = string.Empty;
		private static DateTime roundStartedUtc = DateTime.MinValue;
		private static DateTime emergencyAvailableUtc = DateTime.MinValue;
		private static DateTime meetingTransitionGraceUntilUtc = DateTime.MinValue;
		private static DateTime lastMassDisconnectAlertUtc = DateTime.MinValue;
		private static readonly Queue<DateTime> RecentUnexpectedDisconnects = new Queue<DateTime>();

		public static bool Enabled { get; set; } = true;
		public static bool CheckSpoofedPlatforms { get; set; } = true;
		public static bool EnhancedMenuBehaviorChecks { get; set; } = true;
		public static bool SendNotification { get; set; } = true;
		public static bool DiscardInvalidMessages { get; set; } = true;
		public static int EnforcementThreshold { get; set; } = 100;
		public static float NotificationDuration { get; set; } = 10.0f;
		public static int TrackedPlayerCount { get { EnsureSession(); return Violations.Count; } }

		public enum Punishments
		{
			None,
			Kick,
			Ban
		}

		public static Punishments Punishment { get; set; } = Punishments.Ban;

		public static readonly Dictionary<GameDataTypes, GameDataCheck> GameDataHandlers = new Dictionary<GameDataTypes, GameDataCheck>
		{
			{ GameDataTypes.SceneChangeFlag, new SceneChange() },
			{ GameDataTypes.ReadyFlag, new ClientReady() }
		};

		public static readonly Dictionary<RpcCalls, RpcCheck> RpcHandlers = new Dictionary<RpcCalls, RpcCheck>
		{
			{ RpcCalls.SpiritGuideMessage, new SpiritGuideMessage() },
			{ RpcCalls.QueueOverruleVotes, new QueueOverruleVotes() },
			{ RpcCalls.PlayAnimation, new PlayAnimation() },
			{ RpcCalls.CompleteTask, new CompleteTask() },
			{ RpcCalls.SyncSettings, new SyncSettings() },
			{ RpcCalls.Exiled, new Exiled() },
			{ RpcCalls.CheckName, new CheckName() },
			{ RpcCalls.SetName, new SetName() },
			{ RpcCalls.CheckColor, new CheckColor() },
			{ RpcCalls.SetColor, new SetColor() },
			{ RpcCalls.SetHatStr, new CosmeticChange(RpcCalls.SetHatStr, "hat") },
			{ RpcCalls.SetSkinStr, new CosmeticChange(RpcCalls.SetSkinStr, "skin") },
			{ RpcCalls.SetPetStr, new CosmeticChange(RpcCalls.SetPetStr, "pet") },
			{ RpcCalls.SetVisorStr, new CosmeticChange(RpcCalls.SetVisorStr, "visor") },
			{ RpcCalls.SetNamePlateStr, new CosmeticChange(RpcCalls.SetNamePlateStr, "nameplate") },
			{ RpcCalls.ReportDeadBody, new ReportDeadBody() },
			{ RpcCalls.MurderPlayer, new MurderPlayer() },
			{ RpcCalls.SendChat, new SendChat() },
			{ RpcCalls.StartMeeting, new StartMeeting() },
			{ RpcCalls.SetScanner, new SetScanner() },
			{ RpcCalls.SetStartCounter, new SetStartCounter() },
			{ RpcCalls.EnterVent, new EnterVent() },
			{ RpcCalls.ExitVent, new ExitVent() },
			{ RpcCalls.SnapTo, new SnapTo() },
			{ RpcCalls.CloseMeeting, new CloseMeeting() },
			{ RpcCalls.VotingComplete, new VotingComplete() },
			{ RpcCalls.ClearVote, new ClearVote() },
			{ RpcCalls.CastVote, new CastVoteCheck() },
			{ RpcCalls.AddVote, new AddVote() },
			{ RpcCalls.CloseDoorsOfType, new CloseDoorsOfType() },
			{ RpcCalls.ClimbLadder, new ClimbLadder() },
			{ RpcCalls.UsePlatform, new UsePlatform() },
			{ RpcCalls.BootFromVent, new BootFromVent() },
			{ RpcCalls.UpdateSystem, new UpdateSystem() },
			{ RpcCalls.SetLevel, new SetLevel() },
			{ RpcCalls.SetRole, new SetRole() },
			{ RpcCalls.ProtectPlayer, new ProtectPlayer() },
			{ RpcCalls.Shapeshift, new Shapeshift() },
			{ RpcCalls.CheckMurder, new CheckMurder() },
			{ RpcCalls.CheckProtect, new CheckProtect() },
			{ RpcCalls.CheckShapeshift, new CheckShapeshift() },
			{ RpcCalls.TriggerSpores, new TriggerSpores() },
			{ RpcCalls.CheckVanish, new CheckVanish() },
			{ RpcCalls.StartVanish, new StartVanish() },
			{ RpcCalls.CheckAppear, new CheckAppear() },
			{ RpcCalls.StartAppear, new StartAppear() }
		};

		[HarmonyPatch(typeof(GameManager), nameof(GameManager.StartGame))]
		private static class OnRoundStarted
		{
			private static void Postfix()
			{
				ResetSession();
				SenderEvidence.PauseSequences(null, 15);
				roundStartedUtc = DateTime.UtcNow;
				StartEmergencyCooldown();
				RoundReports.Begin();
			}
		}

		[HarmonyPatch(typeof(ExileController), nameof(ExileController.WrapUp))]
		private static class OnMeetingFinished
		{
			private static void Postfix()
			{
				StartEmergencyCooldown();
				GrantMeetingTransitionGrace(3);
				foreach(var player in PlayerControl.AllPlayerControls) GrantMovementGrace(player, 4);
			}
		}

		[HarmonyPatch(typeof(GameData), nameof(GameData.ShowNotification))]
		private static class OnDisconnectNotification
		{
			private static void Postfix(string playerName, DisconnectReasons reason)
			{
				ObserveDisconnect(playerName, reason);
			}
		}

		[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnPlayerLeft))]
		private static class OnPlayerLeft
		{
			private static DateTime windowStart;
			private static int logged;
			private static void Prefix(ClientData __0, DisconnectReasons __1)
			{
				if(__0 == null) return;
				DateTime now = DateTime.UtcNow;
				if((now - windowStart).TotalSeconds >= 5) { windowStart = now; logged = 0; }
				if(++logged > 32) return; // Bound diagnostic output during a raid.
				var client = AmongUsClient.Instance;
				AUAnticheatPlugin.Log.LogWarning($"[Departure {now:O}] Affected: {PublicText.Name(__0.PlayerName)} (lobby ID {__0.Id}); reason={__1}; localHost={client?.AmHost}; state={client?.GameState}; sender=UNKNOWN. Last local packet block (may be unrelated): {PacketPreflight.LastBlockReason}.");
			}
			// Bind by argument index, not the generated interop parameter name.
			private static void Postfix(ClientData __0)
			{
				if(__0 == null) return;
				Violations.Remove(__0.Id);
				SenderEvidence.ForgetSequences(__0.Id);
				ActivityTracker.Forget(__0.Id);
				MovementMonitor.Forget(__0.Id);
				LobbyMovementGuard.Forget(__0.Id);
				VoteKickGuard.Forget(__0.Id);
				LobbyLevelPolicy.Forget(__0.Id);
			}
		}

		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
		private static class OnPlayerControlRpc
		{
			private static bool Prefix(PlayerControl __instance, byte callId, MessageReader reader)
			{
				return HandleRpc(typeof(PlayerControl), __instance, (RpcCalls)callId, reader);
			}
		}

		[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.HandleRpc))]
		private static class OnPlayerPhysicsRpc
		{
			private static bool Prefix(PlayerPhysics __instance, byte callId, MessageReader reader)
			{
				return HandleRpc(typeof(PlayerPhysics), __instance.myPlayer, (RpcCalls)callId, reader);
			}
		}

		[HarmonyPatch(typeof(CustomNetworkTransform), nameof(CustomNetworkTransform.HandleRpc))]
		private static class OnNetworkTransformRpc
		{
			private static bool Prefix(CustomNetworkTransform __instance, byte callId, MessageReader reader)
			{
				return HandleRpc(typeof(CustomNetworkTransform), __instance.myPlayer, (RpcCalls)callId, reader);
			}
		}

		[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.HandleRpc))]
		private static class OnShipStatusRpc
		{
			private static bool Prefix(byte callId, MessageReader reader)
			{
				return HandleRpc(typeof(ShipStatus), null, (RpcCalls)callId, reader);
			}
		}

		[HarmonyPatch(typeof(VoteBanSystem), nameof(VoteBanSystem.HandleRpc))]
		private static class OnVoteBanRpc
		{
			private static bool Prefix(byte callId, MessageReader reader)
			{
				return HandleRpc(typeof(VoteBanSystem), null, (RpcCalls)callId, reader);
			}
		}

		[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.HandleRpc))]
		private static class OnMeetingRpc
		{
			private static bool Prefix(byte callId, MessageReader reader)
			{
				return HandleRpc(typeof(MeetingHud), null, (RpcCalls)callId, reader);
			}
		}

		[HarmonyPatch(typeof(GameManager), nameof(GameManager.HandleRpc))]
		private static class OnGameManagerRpc
		{
			private static bool Prefix(byte callId, MessageReader reader)
			{
				return HandleRpc(typeof(GameManager), null, (RpcCalls)callId, reader);
			}
		}

		[HarmonyPatch(typeof(NetworkedPlayerInfo), nameof(NetworkedPlayerInfo.HandleRpc))]
		private static class OnPlayerInfoRpc
		{
			private static bool Prefix(NetworkedPlayerInfo __instance, byte callId)
			{
				if(!Enabled || callId != (byte)RpcCalls.SetTasks) return true;
				if(AmongUsClient.Instance == null) return true;
				// Server task assignments can precede local role/round replication.
				if(Utilities.IsAnticheatPresent()) return true;

				PlayerControl target = __instance.Object;
				using var evidence = SenderEvidence.Begin("Player tasks", callId, target?.NetId, target?.OwnerId, SafeName(target));
				if(AmongUsClient.Instance.AmHost && !Utilities.IsAnticheatPresent())
				{
					Flag("Blocked a remote host-only SetTasks message. The affected player object does not identify its sender; no player penalized.");
					return false;
				}

				if(LobbyBehaviour.Instance == null && IsRoundStartGrace()) return true;

				string timing = LobbyBehaviour.Instance != null ? "in the lobby" : "after the assignment window";
				Flag($"Blocked a task assignment for {SafeName(target)} {timing}. Sender unverified; no player penalized.");
				return false;
			}
		}

		private static bool HandleRpc(Type sourceNetObject, PlayerControl player, RpcCalls rpc, MessageReader reader)
		{
			EnsureSession();
			using var evidence = SenderEvidence.Begin(sourceNetObject.Name, (byte)rpc,
				player != null ? player.NetId : null, player != null ? player.OwnerId : null,
				player != null ? SafeName(player) + "'s player object (not proof of who sent it)" : "shared " + sourceNetObject.Name + " object; individual target unknown", rpc.ToString());
			if(!Enabled || !EnhancedMenuBehaviorChecks || (player != null && player.Data == null) ||
				MeetingHud.Instance != null || HasMeetingTransitionGrace() ||
				(GameManager.Instance != null && GameManager.Instance.GameHasStarted && IsRoundStartGrace()))
				SenderEvidence.PauseSequences(player?.OwnerId, 2);
			if(player != null) ActivityTracker.Observe(player.OwnerId, player.NetId, true);
			return HandleRpcCore(sourceNetObject, player, rpc, reader);
		}

		private static bool HandleRpcCore(Type sourceNetObject, PlayerControl player, RpcCalls rpc, MessageReader reader)
		{
			// Structural protection runs before authority/grace fast paths and remains
			// available even with semantic detection switched off.
			if(AUAnticheat.features.Protections.PreflightPackets && !PacketPreflight.ValidateRpc(rpc, reader)) return false;
			NoteRpcTarget(player, rpc, reader);
			if(sourceNetObject == typeof(VoteBanSystem) && rpc == RpcCalls.AddVote &&
				AUAnticheat.features.Protections.FilterVoteKickReplays && !VoteKickGuard.Validate(reader, id => SafeName(AmongUsClient.Instance?.FindClientById(id)?.Character))) return false;
			if(!Enabled || !RpcHandlers.TryGetValue(rpc, out RpcCheck rpcCheck) || !rpcCheck.Enabled) return true;

			// Player network objects can exist briefly before their replicated Data is
			// initialized. State-based validation during that window would turn normal
			// join/scene ordering into a malformed-message conviction.
			if(player != null && player.Data == null) return true;
			if(player != null && AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost &&
				Violations.TryGetValue(player.OwnerId, out ViolationState pendingRemoval) && pendingRemoval.Punished) return false;

			if(sourceNetObject != rpcCheck.GetExpectedNetObject())
			{
				Flag($"Blocked {rpc} on an invalid network object; its owner is not proof of sender identity.");
				return false;
			}

			// In official server-authoritative games these messages are results already
			// approved by the server. The lobby host is only another client and its local
			// role, death, door, meeting, or animation snapshot can be one packet behind.
			// Re-validating those results here caused legitimate kills, door closures,
			// shapeshifts and Phantom transitions to be dropped. Request-side checks still
			// run, and private/modded games where this client is authority still enforce
			// host-only direction below.
			if(Utilities.IsAnticheatPresent() && rpcCheck.IsHostOnly())
			{
				GrantAuthoritativeTransitionGrace(player, rpc);
				return true;
			}

			// On official/server-authoritative lobbies, the lobby host is still a normal
			// client and legitimately receives server-relayed MurderPlayer, Phantom,
			// door and other result RPCs owned by remote player objects. Enforce
			// host-only direction only when this client is the actual network authority.
			bool localIsNetworkAuthority = AmongUsClient.Instance.AmHost && !Utilities.IsAnticheatPresent();
			if(player != null && localIsNetworkAuthority && rpcCheck.IsHostOnly())
			{
				Flag($"Blocked a remote host-only {rpc} result. Its affected player object does not identify the sender; no player penalized.");
				return false;
			}

			if(player == null && localIsNetworkAuthority && rpcCheck.IsHostOnly())
			{
				Flag($"Blocked the host-only {rpc} message from a remote source; this shared-object RPC has no safe sender attribution.");
				return false;
			}

			// The first valid report can create MeetingHud before other reports and
			// gameplay requests that were already in flight are delivered. Do not count
			// those reordered packets as floods or cheats.
			if(rpc == RpcCalls.ReportDeadBody && (MeetingHud.Instance != null || HasMeetingTransitionGrace())) return false;
			if(localIsNetworkAuthority && HasMeetingTransitionGrace() && IsMeetingBoundaryAction(rpc)) return false;

			// Scan-stop is idempotent cleanup after closing a task, dying or meeting.
			// Do not leave the animation stuck because earlier starts exhausted a budget.
			bool scannerCleanup = false;
			if(rpc == RpcCalls.SetScanner && reader != null && reader.BytesRemaining >= 2)
			{
				int position = reader.Position;
				try { scannerCleanup = !reader.ReadBoolean(); }
				finally { reader.Position = position; }
			}
			if(!scannerCleanup && ObserveRpcBurst(player, rpc)) return false;

			int oldPosition = reader.Position;
			// Identity/chat fields and object ownership do not authenticate the
			// network sender. Never let a forged name/ID convict its apparent owner.
			bool sharedObject = player == null || IsAmbiguousIdentityBurst(rpc) || rpc == RpcCalls.SendChat;
			if(sharedObject) unattributedValidationDepth++;
			try
			{
				bool valid = rpcCheck.Validate(player, reader);
				reader.Position = oldPosition;
				return valid || !DiscardInvalidMessages;
			}
			catch(Exception exception)
			{
				reader.Position = oldPosition;
				AUAnticheatPlugin.Log.LogWarning($"Blocked malformed {rpc} message: {exception.GetType().Name}");
				Flag($"Blocked a {rpc} validation failure ({exception.GetType().Name}); this is not proof against the object owner.");
				return false;
			}
			finally
			{
				if(sharedObject) unattributedValidationDepth--;
				reader.Position = oldPosition;
			}
		}

		public static bool HandleGameData(GameDataTypes type, MessageReader reader)
		{
			EnsureSession();
			if(!Enabled || !GameDataHandlers.TryGetValue(type, out GameDataCheck check) || !check.Enabled) return true;

			int oldPosition = reader.Position;
			unattributedValidationDepth++;
			try
			{
				bool valid = check.Validate(reader);
				reader.Position = oldPosition;
				return valid || !DiscardInvalidMessages;
			}
			catch(Exception exception)
			{
				reader.Position = oldPosition;
				AUAnticheatPlugin.Log.LogWarning($"Blocked malformed {type} game-data message: {exception.GetType().Name}");
				Flag($"Blocked a malformed {type} game-data message.");
				return false;
			}
			finally
			{
				unattributedValidationDepth--;
				reader.Position = oldPosition;
			}
		}

		private static void NoteRpcTarget(PlayerControl player, RpcCalls rpc, MessageReader reader)
		{
			if(reader == null) return;
			int position = reader.Position;
			try
			{
				if(rpc == RpcCalls.MurderPlayer || rpc == RpcCalls.CheckMurder || rpc == RpcCalls.ProtectPlayer || rpc == RpcCalls.CheckProtect)
				{
					var target = reader.ReadNetObject<PlayerControl>();
					SenderEvidence.NoteTarget(target == null ? "unknown player target (unresolved reference)" : SafeName(target));
				}
				else if(rpc == RpcCalls.UpdateSystem || rpc == RpcCalls.CloseDoorsOfType || rpc == RpcCalls.StartMeeting || rpc == RpcCalls.SyncSettings)
					SenderEvidence.NoteTarget("shared lobby/game state; potentially all players");
				else if(rpc == RpcCalls.Shapeshift || rpc == RpcCalls.CheckShapeshift)
				{
					var appearance = reader.ReadNetObject<PlayerControl>();
					SenderEvidence.NoteTarget(SafeName(player) + "'s appearance; copied appearance reference: " + SafeName(appearance));
				}
			}
			catch(Exception) { SenderEvidence.NoteTarget("unknown (target could not be read)"); }
			finally { reader.Position = position; }
		}

		public static void Flag(PlayerControl player, string reason, bool shouldPunish = true)
		{
			bool unverified = SenderEvidence.Current != null;
			EnsureSession();
			if(unverified)
			{
				SenderEvidence.NoteCandidate(player != null ? player.OwnerId : null, shouldPunish ? 100 : 50);
				Flag($"Unverified attribution: named players are only affected objects or claims. No score or ban: {reason}");
				return;
			}
			if(unattributedValidationDepth > 0)
			{
				Flag($"Sender unverified; any named player is only a claimed actor. No score or ban: {reason}");
				return;
			}
			if(player == PlayerControl.LocalPlayer) return;

			if(SendNotification) AUAnticheatPlugin.notifications.Send(NotificationTitle, reason, NotificationDuration);
			if(player == null || player.Data == null) return;

			int weight = shouldPunish ? 100 : 50;
			ViolationState state = GetOrCreateState(player.OwnerId);
			DateTime now = DateTime.UtcNow;
			if((now - state.LastSeenUtc).TotalSeconds > 45)
			{
				state.Score = 0;
				state.Count = 0;
				state.Punished = false;
				state.Shared = false;
			}

			state.Score = Math.Min(1000, state.Score + weight);
			state.Count++;
			state.LastSeenUtc = now;
			AddIncident($"{now:HH:mm:ss} | {SafeName(player)} | score {state.Score} | {reason}");

			if(AmongUsClient.Instance.AmHost && !state.Punished && state.Score >= EnforcementThreshold)
			{
				string name = SafeName(player);
				state.Punished = Punish(player);
				if(!state.Shared) { LobbyChat.EvidenceNotice(name, state.Punished ? Punishment == Punishments.Ban ? "banned" : "kicked" : "detected", reason); state.Shared = true; }
			}
		}

		public static void FlagConfirmedExploit(PlayerControl player, string reason)
		{
			bool unverified = SenderEvidence.Current != null;
			EnsureSession();
			if(unverified)
			{
				SenderEvidence.NoteCandidate(player != null ? player.OwnerId : null, 100);
				Flag($"Exploit-pattern observation; sender unknown. No score or ban: {reason}");
				return;
			}
			if(unattributedValidationDepth > 0)
			{
				Flag($"Exploit-pattern message blocked; sender unverified. No score or ban: {reason}");
				return;
			}
			if(player == PlayerControl.LocalPlayer) return;

			DateTime now = DateTime.UtcNow;
			if(player == null || player.Data == null)
			{
				AddIncident($"{now:HH:mm:ss} | unattributed confirmed signature | {reason}");
				if(SendNotification) AUAnticheatPlugin.notifications.Send("Anti-cheat", $"Blocked a confirmed exploit signature, but the packet did not contain a trustworthy player identity: {reason}", NotificationDuration);
				return;
			}

			ViolationState state = GetOrCreateState(player.OwnerId);
			// A concrete, attributable menu action is exactly one 100-point
			// violation. It still receives immediate normal host enforcement, so
			// increasing the stored score to an artificial sentinel is unnecessary.
			state.Score = Math.Min(1000, state.Score + 100);
			state.Count++;
			state.LastSeenUtc = now;
			AddIncident($"{now:HH:mm:ss} | {SafeName(player)} | +100 confirmed menu action | score {state.Score} | {reason}");
			if(SendNotification) AUAnticheatPlugin.notifications.Send(NotificationTitle, $"Confirmed exploit blocked: {reason}", NotificationDuration);

			if(AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost && !state.Punished)
			{
				string name = SafeName(player);
				state.Punished = BanConfirmedExploit(player);
				if(!state.Shared) { LobbyChat.EvidenceNotice(name, state.Punished ? "banned" : "detected", reason); state.Shared = true; }
			}
		}

		public static void Flag(string reason)
		{
			EnsureSession();
			SenderEvidence.Record();
			AddIncident($"{DateTime.UtcNow:HH:mm:ss} | unattributed | {reason}");
			if(SendNotification) AUAnticheatPlugin.notifications.Send(NotificationTitle, reason, NotificationDuration);
		}

		private static string NotificationTitle => AmongUsClient.Instance != null && !AmongUsClient.Instance.AmHost
			? "Anti-cheat (non-host alert)" : "Anti-cheat";

		public static bool ObserveRpcBurst(PlayerControl player, RpcCalls rpc)
		{
			if(player == null || player == PlayerControl.LocalPlayer || player.Data == null) return false;

			ViolationState state = GetOrCreateState(player.OwnerId);
			DateTime now = DateTime.UtcNow;
			Prune(state.RecentMessages, now, 2);
			FloodSafety.Enqueue(state.RecentMessages, now);

			(int maxEvents, double windowSeconds) = GetRateLimit(rpc);
			if(!state.MessagesByType.TryGetValue(rpc, out Queue<DateTime> messages))
			{
				messages = new Queue<DateTime>();
				state.MessagesByType.Add(rpc, messages);
			}

			Prune(messages, now, windowSeconds);
			FloodSafety.Enqueue(messages, now);

			bool globalBurst = state.RecentMessages.Count > 120;
			bool typeBurst = messages.Count > maxEvents;
			bool categoryBurst = false;
			bool extremeCategoryBurst = false;
			string categoryName = null;

			if(EnhancedMenuBehaviorChecks)
			{
				(string Name, int MaxEvents, double WindowSeconds) category = GetCategoryRateLimit(rpc);
				categoryName = category.Name;
				if(categoryName != null)
				{
					if(!state.MessagesByCategory.TryGetValue(categoryName, out Queue<DateTime> categoryMessages))
					{
						categoryMessages = new Queue<DateTime>();
						state.MessagesByCategory.Add(categoryName, categoryMessages);
					}

					Prune(categoryMessages, now, category.WindowSeconds);
					FloodSafety.Enqueue(categoryMessages, now);
					categoryBurst = categoryMessages.Count > category.MaxEvents;
					extremeCategoryBurst = categoryMessages.Count > category.MaxEvents + 4;
				}
			}

			bool burst = globalBurst || typeBurst || categoryBurst;
			if(!burst) return false;
			// Reliable task packets can be delivered together after a network stall.
			// Keep valid credits; only stop an extreme overall flood, without a ban
			// inferred from the arrival rate alone.
			if(rpc == RpcCalls.CompleteTask && !globalBurst) return false;

			bool ambiguousIdentityBurst = IsAmbiguousIdentityBurst(rpc);
			bool extremeBurst = !ambiguousIdentityBurst && (state.RecentMessages.Count > 180 || messages.Count > maxEvents + 3 || extremeCategoryBurst);
			double minimumAlertInterval = ambiguousIdentityBurst ? 5.0 : (extremeBurst ? 0.25 : 1.0);
			if((now - state.LastBurstAlertUtc).TotalSeconds >= minimumAlertInterval)
			{
				state.LastBurstAlertUtc = now;
				string scope = categoryBurst && categoryName != null ? $"{categoryName} action" : rpc.ToString();
				Flag($"Blocked a {scope} burst associated with {SafeName(player)}; arrival rate and object ownership alone do not authenticate the sender. No automatic penalty.");
			}

			return true;
		}

		public static bool ObserveDistinctTargetSpray(PlayerControl player, PlayerControl target, RpcCalls rpc)
		{
			if(player == null || player.Data == null || target == null || target.Data == null || player == PlayerControl.LocalPlayer) return false;

			(int MaximumTargets, double WindowSeconds) limits = rpc switch
			{
				RpcCalls.CheckMurder => (4, 2),
				RpcCalls.CheckProtect => (4, 3),
				RpcCalls.CheckShapeshift => (5, 3),
				_ => (0, 0)
			};
			if(limits.MaximumTargets == 0) return false;

			ViolationState state = GetOrCreateState(player.OwnerId);
			if(!state.TargetedActionsByType.TryGetValue(rpc, out Queue<TargetedAction> actions))
			{
				actions = new Queue<TargetedAction>();
				state.TargetedActionsByType.Add(rpc, actions);
			}

			DateTime now = DateTime.UtcNow;
			while(actions.Count > 0 && (now - actions.Peek().TimeUtc).TotalSeconds > limits.WindowSeconds) actions.Dequeue();
			FloodSafety.Enqueue(actions, new TargetedAction { TargetPlayerId = target.PlayerId, TimeUtc = now });

			HashSet<byte> distinctTargets = new HashSet<byte>();
			foreach(TargetedAction action in actions) distinctTargets.Add(action.TargetPlayerId);
			if(distinctTargets.Count < limits.MaximumTargets) return false;

			actions.Clear();
			string reason = $"{SafeName(player)} targeted {distinctTargets.Count} different players with {rpc} within {limits.WindowSeconds:F0} seconds.";
			if(rpc == RpcCalls.CheckMurder)
			{
				FlagConfirmedExploit(player, reason);
			}
			else
			{
				Flag(player, reason);
			}
			return true;
		}

		public static bool ObserveRapidColorCycling(PlayerControl player, byte color)
		{
			if(player == null || player.Data == null || player == PlayerControl.LocalPlayer) return false;

			ViolationState state = GetOrCreateState(player.OwnerId);
			DateTime now = DateTime.UtcNow;
			while(state.RecentColorRequests.Count > 0 && (now - state.RecentColorRequests.Peek().TimeUtc).TotalSeconds > 2)
			{
				state.RecentColorRequests.Dequeue();
			}

			FloodSafety.Enqueue(state.RecentColorRequests, new ColorRequest { Color = color, TimeUtc = now });
			HashSet<byte> distinctColors = new HashSet<byte>();
			foreach(ColorRequest request in state.RecentColorRequests) distinctColors.Add(request.Color);

			if(distinctColors.Count >= 10)
			{
				state.RecentColorRequests.Clear();
				FlagConfirmedExploit(player, $"{SafeName(player)} cycled through {distinctColors.Count} different colors within two seconds.");
				return true;
			}

			if(distinctColors.Count == 8)
			{
				Flag(player, $"{SafeName(player)} rapidly cycled through eight different colors; blocked pending further evidence.", false);
				return true;
			}

			return false;
		}

		public static bool ObserveTaskCompletionSweep(PlayerControl player, uint taskIndex)
		{
			if(!EnhancedMenuBehaviorChecks || player == null || player.Data == null || player == PlayerControl.LocalPlayer ||
				IsRoundStartGrace() || MeetingHud.Instance != null || HasMeetingTransitionGrace()) return false;

			ViolationState state = GetOrCreateState(player.OwnerId);
			DateTime now = DateTime.UtcNow;
			PruneIndexed(state.RecentTaskCompletions, now, 1.0);
			FloodSafety.Enqueue(state.RecentTaskCompletions, new IndexedAction { Value = unchecked((int)taskIndex), TimeUtc = now });

			HashSet<int> distinctTasks = new HashSet<int>();
			foreach(IndexedAction action in state.RecentTaskCompletions) distinctTasks.Add(action.Value);
			if(distinctTasks.Count < 5) return false;

			state.RecentTaskCompletions.Clear();
			// Delivery timestamps cannot distinguish automation from a backlog after
			// lag. Preserve legitimate task credits and use this only as a review alert.
			Flag($"Task burst observed for {SafeName(player)}: {distinctTasks.Count} distinct completions arrived within one second. Possible automation or network catch-up; no automatic penalty.");
			return false;
		}

		public static bool ObserveSabotageSweep(PlayerControl player, SystemTypes sabotageTarget)
		{
			if(!EnhancedMenuBehaviorChecks || player == null || player.Data == null || player == PlayerControl.LocalPlayer ||
				IsRoundStartGrace() || MeetingHud.Instance != null || HasMeetingTransitionGrace()) return false;

			ViolationState state = GetOrCreateState(player.OwnerId);
			DateTime now = DateTime.UtcNow;
			PruneIndexed(state.RecentSabotageRequests, now, 1.5);
			FloodSafety.Enqueue(state.RecentSabotageRequests, new IndexedAction { Value = (int)sabotageTarget, TimeUtc = now });

			HashSet<int> distinctTargets = new HashSet<int>();
			foreach(IndexedAction action in state.RecentSabotageRequests) distinctTargets.Add(action.Value);
			if(distinctTargets.Count < 2) return false;

			state.RecentSabotageRequests.Clear();
			Flag($"Multiple sabotage requests observed: {distinctTargets.Count} targets within 1.5 seconds. Claimed actor {SafeName(player)} is unverified; the game must validate cooldown and authority. No automatic penalty.");
			return false;
		}

		private static bool IsAmbiguousIdentityBurst(RpcCalls rpc)
		{
			return rpc == RpcCalls.CheckName || rpc == RpcCalls.SetName || rpc == RpcCalls.CheckColor || rpc == RpcCalls.SetColor ||
				rpc == RpcCalls.SetLevel || rpc == RpcCalls.SetHatStr || rpc == RpcCalls.SetSkinStr || rpc == RpcCalls.SetPetStr ||
				rpc == RpcCalls.SetVisorStr || rpc == RpcCalls.SetNamePlateStr;
		}

		private static void GrantAuthoritativeTransitionGrace(PlayerControl player, RpcCalls rpc)
		{
			switch(rpc)
			{
				case RpcCalls.BootFromVent:
				case RpcCalls.SetRole:
				case RpcCalls.Shapeshift:
				case RpcCalls.StartVanish:
				case RpcCalls.StartAppear:
					GrantMovementGrace(player, 4);
					break;
				case RpcCalls.StartMeeting:
				case RpcCalls.CloseMeeting:
				case RpcCalls.VotingComplete:
					GrantMeetingTransitionGrace(3);
					foreach(PlayerControl candidate in PlayerControl.AllPlayerControls)
					{
						GrantMovementGrace(candidate, 5);
					}
					break;
			}
		}

		private static bool IsMeetingBoundaryAction(RpcCalls rpc)
		{
			return rpc == RpcCalls.CheckMurder || rpc == RpcCalls.CheckProtect || rpc == RpcCalls.CheckShapeshift ||
				rpc == RpcCalls.CheckVanish || rpc == RpcCalls.CheckAppear || rpc == RpcCalls.EnterVent ||
				rpc == RpcCalls.ExitVent || rpc == RpcCalls.ClimbLadder || rpc == RpcCalls.UsePlatform ||
				rpc == RpcCalls.TriggerSpores;
		}

		public static int GetScore(int ownerId)
		{
			EnsureSession();
			return Violations.TryGetValue(ownerId, out ViolationState state) ? state.Score : 0;
		}

		public static bool IsRoundStartGrace()
		{
			if(GameManager.Instance == null || !GameManager.Instance.GameHasStarted) return true;
			if(roundStartedUtc == DateTime.MinValue) roundStartedUtc = DateTime.UtcNow;
			return (DateTime.UtcNow - roundStartedUtc).TotalSeconds <= 15;
		}

		public static bool IsEmergencyMeetingDuringCooldown(out double secondsRemaining)
		{
			secondsRemaining = 0;
			if(GameManager.Instance == null || !GameManager.Instance.GameHasStarted) return false;
			if(emergencyAvailableUtc == DateTime.MinValue) StartEmergencyCooldown();
			if(emergencyAvailableUtc == DateTime.MinValue) return false;

			secondsRemaining = (emergencyAvailableUtc - DateTime.UtcNow).TotalSeconds;
			// Allow a small replication/timer-display margin. We only punish a clearly
			// impossible emergency meeting, not a request racing the final countdown tick.
			return secondsRemaining > 1.5;
		}

		private static void StartEmergencyCooldown()
		{
			try
			{
				int cooldown = GameOptionsManager.Instance.CurrentGameOptions.GetInt(Int32OptionNames.EmergencyCooldown);
				emergencyAvailableUtc = DateTime.UtcNow.AddSeconds(Math.Max(0, cooldown));
			}
			catch(Exception exception)
			{
				emergencyAvailableUtc = DateTime.MinValue;
				AUAnticheatPlugin.Log.LogWarning($"Could not read the emergency-meeting cooldown: {exception.GetType().Name}");
			}
		}

		public static void GrantMeetingTransitionGrace(double seconds = 3)
		{
			SenderEvidence.PauseSequences(null, seconds);
			DateTime until = DateTime.UtcNow.AddSeconds(seconds);
			if(until > meetingTransitionGraceUntilUtc) meetingTransitionGraceUntilUtc = until;
		}

		public static bool HasMeetingTransitionGrace()
		{
			return meetingTransitionGraceUntilUtc >= DateTime.UtcNow;
		}

		public static PlayerControl GetHostPlayer()
		{
			if(AmongUsClient.Instance == null) return null;
			ClientData host = AmongUsClient.Instance.FindClientById(AmongUsClient.Instance.HostId);
			return host == null ? null : host.Character;
		}

		public static PlayerControl GetAuthoritativeActor(PlayerControl objectOwner)
		{
			return AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost ? objectOwner : GetHostPlayer();
		}

		public static void GrantMovementGrace(PlayerControl player, double seconds = 2)
		{
			if(player == null || player.Data == null) return;
			SenderEvidence.PauseSequences(player.OwnerId, seconds);
			ViolationState state = GetOrCreateState(player.OwnerId);
			DateTime until = DateTime.UtcNow.AddSeconds(seconds);
			if(until > state.MovementGraceUntilUtc) state.MovementGraceUntilUtc = until;
		}

		public static bool HasMovementGrace(PlayerControl player)
		{
			if(player == null || player.Data == null) return true;
			return GetOrCreateState(player.OwnerId).MovementGraceUntilUtc >= DateTime.UtcNow;
		}

		public static void GrantGhostRoleGrace(PlayerControl player, double seconds = 8)
		{
			if(player == null || player.Data == null) return;
			SenderEvidence.PauseSequences(player.OwnerId, seconds);
			ViolationState state = GetOrCreateState(player.OwnerId);
			DateTime until = DateTime.UtcNow.AddSeconds(seconds);
			if(until > state.GhostRoleGraceUntilUtc) state.GhostRoleGraceUntilUtc = until;
		}

		public static bool HasGhostRoleGrace(PlayerControl player)
		{
			if(player == null || player.Data == null) return false;
			return GetOrCreateState(player.OwnerId).GhostRoleGraceUntilUtc >= DateTime.UtcNow;
		}

		public static void GrantVentCleaningGrace(PlayerControl player, int ventId, double seconds = 8)
		{
			if(player == null || player.Data == null) return;
			SenderEvidence.PauseSequences(player.OwnerId, seconds);
			ViolationState state = GetOrCreateState(player.OwnerId);
			state.VentCleaningVentId = ventId;
			state.VentCleaningGraceUntilUtc = DateTime.UtcNow.AddSeconds(seconds);
		}

		public static bool HasVentCleaningGrace(PlayerControl player, int ventId)
		{
			if(player == null || player.Data == null) return false;
			ViolationState state = GetOrCreateState(player.OwnerId);
			return state.VentCleaningVentId == ventId && state.VentCleaningGraceUntilUtc >= DateTime.UtcNow;
		}

		public static bool TryConsumeVentCleaningGrace(PlayerControl player, int ventId)
		{
			if(!HasVentCleaningGrace(player, ventId)) return false;
			ViolationState state = GetOrCreateState(player.OwnerId);
			state.VentCleaningVentId = -1;
			state.VentCleaningGraceUntilUtc = DateTime.MinValue;
			return true;
		}

		private static void ObserveDisconnect(string playerName, DisconnectReasons reason)
		{
			if(reason == DisconnectReasons.ExitGame || reason == DisconnectReasons.Kicked || reason == DisconnectReasons.Banned) return;

			EnsureSession();
			DateTime now = DateTime.UtcNow;
			Prune(RecentUnexpectedDisconnects, now, 2.5);
			FloodSafety.Enqueue(RecentUnexpectedDisconnects, now);
			if(RecentUnexpectedDisconnects.Count < 3 || (now - lastMassDisconnectAlertUtc).TotalSeconds < 8) return;

			lastMassDisconnectAlertUtc = now;
			RecentUnexpectedDisconnects.Clear();
			Flag($"Detected several unexpected disconnects in under three seconds (latest: {playerName}, {reason}). This can indicate a false-error-kick attempt or a server outage; no player was blamed without sender evidence.");
		}

		public static string GetRecentIncidentSummary(int maximum = 5)
		{
			EnsureSession();
			if(Incidents.Count == 0) return "No incidents recorded in this lobby.";

			int start = Math.Max(0, Incidents.Count - maximum);
			return string.Join("\n", Incidents.GetRange(start, Incidents.Count - start));
		}

		public static string GetSequenceSummary()
		{
			EnsureSession();
			if(!Enabled || !EnhancedMenuBehaviorChecks) return "Related-action analysis is off. Enable Cheat detection and Extra behaviour checks in Detection.";
			return SenderEvidence.SequenceSummary(id => SafeName(AmongUsClient.Instance?.FindClientById(id)?.Character));
		}

		public static void ResetSession()
		{
			SenderEvidence.Reset();
			InfluencerProtection.Reset();
			LobbyMovementGuard.Reset();
			LobbyLevelPolicy.Reset();
			VoteKickGuard.Reset();
			PacketPreflight.Reset();
			MovementMonitor.Reset();
			Violations.Clear();
			Incidents.Clear();
			roundStartedUtc = DateTime.MinValue;
			emergencyAvailableUtc = DateTime.MinValue;
			meetingTransitionGraceUntilUtc = DateTime.MinValue;
			lastMassDisconnectAlertUtc = DateTime.MinValue;
			RecentUnexpectedDisconnects.Clear();
			sessionKey = GetSessionKey();
		}

		private static void EnsureSession()
		{
			string current = GetSessionKey();
			if(current != sessionKey)
			{
				SenderEvidence.Reset();
				InfluencerProtection.Reset();
				VoteKickGuard.Reset();
				PacketPreflight.Reset();
				MovementMonitor.Reset();
				Violations.Clear();
				Incidents.Clear();
				roundStartedUtc = DateTime.MinValue;
				emergencyAvailableUtc = DateTime.MinValue;
				meetingTransitionGraceUntilUtc = DateTime.MinValue;
				lastMassDisconnectAlertUtc = DateTime.MinValue;
				RecentUnexpectedDisconnects.Clear();
				sessionKey = current;
			}
		}

		private static string GetSessionKey()
		{
			if(AmongUsClient.Instance == null) return "offline";
			return $"{AmongUsClient.Instance.GameId}:{AmongUsClient.Instance.HostId}";
		}

		private static ViolationState GetOrCreateState(int ownerId)
		{
			if(!Violations.TryGetValue(ownerId, out ViolationState state))
			{
				state = new ViolationState();
				Violations.Add(ownerId, state);
			}

			return state;
		}

		private static void AddIncident(string incident)
		{
			Incidents.Add(incident);
			if(Incidents.Count > 50) Incidents.RemoveAt(0);
		}

		private static void Prune(Queue<DateTime> events, DateTime now, double windowSeconds)
		{
			while(events.Count > 0 && (now - events.Peek()).TotalSeconds > windowSeconds)
			{
				events.Dequeue();
			}
		}

		private static void PruneIndexed(Queue<IndexedAction> events, DateTime now, double windowSeconds)
		{
			while(events.Count > 0 && (now - events.Peek().TimeUtc).TotalSeconds > windowSeconds)
			{
				events.Dequeue();
			}
		}

		private static (int MaxEvents, double WindowSeconds) GetRateLimit(RpcCalls rpc)
		{
			return rpc switch
			{
				RpcCalls.CompleteTask => (6, 5),
				RpcCalls.ReportDeadBody => (6, 5),
				RpcCalls.SetName => (6, 10),
				RpcCalls.CheckColor or RpcCalls.SetColor => (15, 10),
				RpcCalls.SetPetStr => (5, 10),
				RpcCalls.SetHatStr or RpcCalls.SetSkinStr or RpcCalls.SetVisorStr or RpcCalls.SetNamePlateStr => (8, 10),
				RpcCalls.SendChat => (4, 5),
				RpcCalls.SetLevel => (3, 30),
				RpcCalls.EnterVent or RpcCalls.ExitVent => (8, 3),
				RpcCalls.SnapTo => (6, 3),
				RpcCalls.CastVote => (3, 5),
				RpcCalls.AddVote => (10, 5),
				RpcCalls.ClimbLadder or RpcCalls.UsePlatform => (6, 3),
				RpcCalls.UpdateSystem => (35, 3),
				RpcCalls.MurderPlayer or RpcCalls.CheckMurder => (4, 2),
				RpcCalls.ProtectPlayer or RpcCalls.Shapeshift => (6, 5),
				RpcCalls.SetRole => (8, 15),
				RpcCalls.TriggerSpores => (20, 3),
				_ => (40, 3)
			};
		}

		private static (string Name, int MaxEvents, double WindowSeconds) GetCategoryRateLimit(RpcCalls rpc)
		{
			return rpc switch
			{
				RpcCalls.CheckName or RpcCalls.SetName or RpcCalls.CheckColor or RpcCalls.SetColor or RpcCalls.SetLevel or
				RpcCalls.SetHatStr or RpcCalls.SetSkinStr or RpcCalls.SetPetStr or RpcCalls.SetVisorStr or RpcCalls.SetNamePlateStr
					=> ("identity/cosmetic", 30, 10),
				RpcCalls.EnterVent or RpcCalls.ExitVent or RpcCalls.SnapTo or RpcCalls.ClimbLadder or RpcCalls.UsePlatform or RpcCalls.BootFromVent
					=> ("movement", 18, 3),
				RpcCalls.CompleteTask or RpcCalls.PlayAnimation or RpcCalls.SetScanner
					=> ("task/animation", 12, 5),
				RpcCalls.MurderPlayer or RpcCalls.CheckMurder or RpcCalls.ProtectPlayer or RpcCalls.CheckProtect or
				RpcCalls.Shapeshift or RpcCalls.CheckShapeshift or RpcCalls.CheckVanish or RpcCalls.StartVanish or RpcCalls.CheckAppear or RpcCalls.StartAppear
					=> ("combat/role", 10, 5),
				RpcCalls.ReportDeadBody or RpcCalls.StartMeeting or RpcCalls.CastVote or RpcCalls.AddVote or RpcCalls.ClearVote or RpcCalls.CloseMeeting or RpcCalls.VotingComplete
					=> ("meeting/vote", 12, 5),
				RpcCalls.UpdateSystem or RpcCalls.CloseDoorsOfType or RpcCalls.TriggerSpores
					=> ("ship/system", 42, 3),
				_ => (null, 0, 0)
			};
		}

		private static bool Punish(PlayerControl player)
		{
			if(Punishment == Punishments.None || AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost ||
				player == null || player.Data == null || player == PlayerControl.LocalPlayer || player.OwnerId == AmongUsClient.Instance.HostId) return false;

			bool ban = Punishment == Punishments.Ban;
			AUAnticheatPlugin.Log.LogMessage($"{SafeName(player)} was {(ban ? "banned" : "kicked")} by AU-Anticheat after reaching the evidence threshold.");
			AUAnticheatPlugin.notifications.Send("AU-Anticheat", $"{SafeName(player)} was {(ban ? "banned" : "kicked")} by anti-cheat.", 5);
			AmongUsClient.Instance.KickPlayer(player.OwnerId, ban);
			return true;
		}

		private static bool BanConfirmedExploit(PlayerControl player)
		{
			if(AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost || player == null || player.Data == null ||
				player == PlayerControl.LocalPlayer || player.OwnerId == AmongUsClient.Instance.HostId) return false;

			AUAnticheatPlugin.Log.LogMessage($"{SafeName(player)} was immediately banned by AU-Anticheat for a confirmed exploit signature.");
			AUAnticheatPlugin.notifications.Send("AU-Anticheat", $"{SafeName(player)} was banned by anti-cheat.", 5);
			AmongUsClient.Instance.KickPlayer(player.OwnerId, true);
			return true;
		}

		private static string SafeName(PlayerControl player)
		{
			return player != null && player.Data != null ? PublicText.Name(player.Data.PlayerName) : "Unknown player";
		}
	}
}
