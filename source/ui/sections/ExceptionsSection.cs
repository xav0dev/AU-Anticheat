using UnityEngine;
using AUAnticheat.anticheat;

namespace AUAnticheat.ui.sections
{
	internal sealed class ExceptionsSection : ISection
	{
		public static bool Editing;
		internal static string Draft = "";
		private static string status = "";
		public ExceptionsSection() : base("Exceptions") { }
		// Input.inputString works without the stripped native GUI text editor.
		public static void Type(string input)
		{
			if(!Editing || string.IsNullOrEmpty(input)) return;
			foreach(char c in input)
			{
				if(c == '\b') { if(Draft.Length > 0) Draft = Draft.Substring(0, Draft.Length - 1); }
				else if(c == '\n' || c == '\r') { Add(); break; }
				else if(c == '\u001b') { Editing = false; break; }
				else if(Draft.Length < 36 && ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '#')) Draft += c;
			}
		}
		private static void Add()
		{
			if(FriendCodeExceptions.Add(Draft)) { Draft = ""; Editing = false; status = "Exception saved."; }
			else status = "Enter a full friend code such as name#1234. It may already be listed, or the list is full (128).";
		}
		public override void Render()
		{
			Styles.Help("Friends who can join below your minimum level. Other anti-cheat checks still apply.");
			if(GUILayout.Button(Editing ? "Stop typing" : "Type friend code")) { Editing = !Editing; LoadoutsEditor.Editing=RulesEditor.Editing=false; }
			GUILayout.Label("Friend code: " + (Draft.Length == 0 ? "name#1234" : Draft) + (Editing ? " |" : ""), Styles.Heading);
			Styles.Help(Editing ? "Enter to save. Backspace to edit. Escape to stop." : "Close game chat before typing. Codes are saved only on this device.");
			if(GUILayout.Button("Add exception")) Add();
			if(GUILayout.Button("Clear input")) Draft = "";
			GUILayout.Label(status);
			Styles.HeadingText($"Saved friends ({FriendCodeExceptions.Snapshot().Length})");
			foreach(string code in FriendCodeExceptions.Snapshot())
				if(GUILayout.Button("Remove " + code)) { FriendCodeExceptions.Remove(code); status = "Exception removed."; }
		}
	}
}
