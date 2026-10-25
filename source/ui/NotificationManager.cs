using System;
using System.Collections.Generic;
using UnityEngine;

namespace AUAnticheat.ui
{
	internal class NotificationManager : MonoBehaviour
	{
		private const int MaximumQueuedNotifications = 32;
		private const int MaximumHistoryEntries = 200;
		private const int MaximumEventsPerSecond = 12;
		public List<Notification> notifications = new List<Notification>();
		public bool DisableNotifications = false;
		public bool ShowTechnicalDetails = false;
		private readonly Dictionary<string, float> lastShownByMessage = new Dictionary<string, float>();
		private readonly List<string> notificationHistory = new List<string>();
		private readonly List<string> notificationDetails = new List<string>();
		private float eventWindowStarted;
		private int eventsInWindow;
		private string historySessionKey = string.Empty;

		public static Vector2 BoxSize
		{
			get { return new Vector2(380, 154) * MainUI.EffectiveScale; }
		}

		public static Vector2 BoxHeaderSize
		{
			get { return new Vector2(BoxSize.x, 17 * MainUI.scale); }
		}

		public static Vector2 BoxContentPadding
		{
			get { return new Vector2(10, 0) * MainUI.scale; }
		}

		public static Vector2 BoxContentSize
		{
			get { return new Vector2(BoxSize.x - BoxContentPadding.x, BoxSize.y - BoxHeaderSize.y - BoxSliderSize.y); }
		}

		public static Vector2 BoxSliderSize
		{
			get { return new Vector2(BoxSize.x, 20 * MainUI.scale); }
		}

		public void Update()
		{
			anticheat.LobbyServices.Tick();
			EnsureHistorySession();
			int notificationCount = Math.Min(GetMaxNotifications(), notifications.Count);

			for(int i = 0; i < notificationCount; i++)
			{
				Notification notification = notifications[i];
				notification.lifetime += Time.deltaTime;

				if(notification.HasExpired)
				{
					notifications.RemoveAt(i);

					// Since we removed an element from the notifications list, we have to decrement both the current notification index
					// and the max notifications to avoid errors from accessing outside the list length
					i--;
					notificationCount--;
					continue;
				}
			}
		}

		public void OnGUI()
		{
			if(DisableNotifications) return;
			using var theme = Styles.UseTheme();

			int notificationCount = Math.Min(GetMaxNotifications(), notifications.Count);

			for(byte i = 0; i < notificationCount; i++)
			{
				RenderNotification(i, notifications[i]);
			}
		}

		[Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
		private void RenderNotification(byte position, Notification notification)
		{
			float s = MainUI.EffectiveScale;
			float boxX = Screen.width - BoxSize.x - 8;
			float boxY = Screen.height - (BoxSize.y + 8*s) * (position + 1);
			GUI.Box(new Rect(boxX, boxY, BoxSize.x, BoxSize.y), "", Styles.MainBox);
			Styles.DrawBadge(new Rect(boxX+12*s,boxY+10*s,24*s,28*s));
			GUI.Label(new Rect(boxX+44*s,boxY+10*s,320*s,28*s),notification.title,Styles.Heading);
			GUI.Label(new Rect(boxX+16*s,boxY+44*s,346*s,98*s),notification.message,Styles.Hint);
		}

		public int GetMaxNotifications()
		{
			return Screen.height / 2 / (int)BoxSize.y;
		}

		// The time to live value for a notification should be five seconds if it is a success message, and ten seconds if it is a failure message
		public void Send(string title, string message, float ttl = 10)
		{
			bool protection = title.StartsWith("Anti-cheat") || title == "Protection";
			string affected = protection ? anticheat.SenderEvidence.TargetSummary : "";
			if(protection) message += "\n" + affected;
			ttl = Math.Max(10, ttl);
			float now = Time.realtimeSinceStartup;

			// Suppressed duplicates must not consume the global event budget; otherwise
			// one repeating warning can hide unrelated notifications from history.
			string duplicateKey = $"{title}\n{message}";
			if(lastShownByMessage.TryGetValue(duplicateKey, out float lastShown) && now - lastShown < 1.0f) return;

			if(now - eventWindowStarted >= 1.0f)
			{
				eventWindowStarted = now;
				eventsInWindow = 0;
			}

			if(eventsInWindow >= MaximumEventsPerSecond) return;
			eventsInWindow++;

			if(lastShownByMessage.Count >= 64) lastShownByMessage.Clear();
			lastShownByMessage[duplicateKey] = now;

			AUAnticheatPlugin.Log.LogMessage($"[Notification] [{title}] {message}");
			EnsureHistorySession();
			notificationDetails.Add($"{DateTime.Now:HH:mm:ss} | {title} | {message}");
			message = anticheat.PublicText.Simple(title, message);
			// Keep the target first on the compact popup, not below text that may clip.
			if(protection)
			{
				if(message.EndsWith("\n" + affected)) message = message.Substring(0,message.Length-affected.Length-1);
				message = affected + "\n" + message;
			}
			notificationHistory.Add($"{DateTime.Now:HH:mm:ss} | {title} | {message}");
			if(notificationHistory.Count > MaximumHistoryEntries) { notificationHistory.RemoveAt(0); notificationDetails.RemoveAt(0); }

			if(DisableNotifications) return;
			if(notifications.Count >= MaximumQueuedNotifications) notifications.RemoveAt(0);

			Notification notification = new Notification(title, message, ttl);
			notifications.Add(notification);
		}

		public void ClearNotifications()
		{
			notifications.Clear();
			lastShownByMessage.Clear();
		}

		public string GetHistorySummary(int maximum = 50)
		{
			EnsureHistorySession();
			if(notificationHistory.Count == 0) return "No notifications recorded in this lobby.";

			int start = Math.Max(0, notificationHistory.Count - maximum);
			return string.Join("\n", (ShowTechnicalDetails ? notificationDetails : notificationHistory).GetRange(start, notificationHistory.Count - start));
		}

		public int HistoryCount
		{
			get { EnsureHistorySession(); return notificationHistory.Count; }
		}

		public void ClearHistory()
		{
			notificationHistory.Clear();
			notificationDetails.Clear();
		}

		private void EnsureHistorySession()
		{
			string current = AmongUsClient.Instance == null ? "offline" : $"{AmongUsClient.Instance.GameId}:{AmongUsClient.Instance.HostId}";
			if(current == historySessionKey) return;

			historySessionKey = current;
			notificationHistory.Clear();
			notificationDetails.Clear();
			lastShownByMessage.Clear();
		}
	}
}
