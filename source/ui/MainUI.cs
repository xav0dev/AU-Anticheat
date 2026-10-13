using AUAnticheat.ui.sections;
using System;
using UnityEngine;

namespace AUAnticheat.ui
{
	public class MainUI : MonoBehaviour
	{
		public bool visible;
		public static float scale = 1;
		public static Vector2 windowPosition = new(90,70);
		public static float EffectiveScale => Math.Max(.1f, Math.Min(scale, Math.Min((Screen.width-24f)/760f,(Screen.height-24f)/600f)));
		public static Vector2 WindowSize => new Vector2(760,600) * EffectiveScale;
		public static Vector2 HeaderSize => new Vector2(760,82) * EffectiveScale;
		public static Vector2 HeaderPosition => windowPosition;
		public static Vector2 SectionListPosition => new(windowPosition.x+16*EffectiveScale,windowPosition.y+94*EffectiveScale);
		public static Vector2 SectionButtonSize => new Vector2(164,47)*EffectiveScale;
		public static Vector2 FeaturePanePosition => new(windowPosition.x+204*EffectiveScale,windowPosition.y+96*EffectiveScale);
		public static Vector2 FeaturePaneSize => new Vector2(532,474)*EffectiveScale;
		private readonly ISection[] sections = { new GeneralSection(), new LobbySection(), new NotificationsSection(), new ModeratorSection(), new ProtectionsSection(), new AnticheatSection(), new MenuSection() };
		private readonly string[] sectionErrors = new string[7];
		public byte activeTab;
		private bool dragging;
		private Vector2 dragOffset;
		public void OnApplicationQuit() { anticheat.SavedSettings.Save(); }
		public void Update()
		{
			anticheat.SavedSettings.Tick();
			if(Input.GetKeyDown(KeyCode.Insert)) visible = !visible;
			if(!visible) { LoadoutsEditor.Editing = ExceptionsSection.Editing = RulesEditor.Editing = false; dragging = false; return; }
			if(activeTab == 1 && LoadoutsEditor.Editing) { LoadoutsEditor.Type(Input.inputString); return; }
			if(activeTab == 1 && RulesEditor.Editing) { RulesEditor.Type(Input.inputString); return; }
			if(activeTab == 1 && ExceptionsSection.Editing) { ExceptionsSection.Type(Input.inputString); return; }
			RulesEditor.Editing = false;
			LoadoutsEditor.Editing = false;
			ExceptionsSection.Editing = false;
			if(Input.GetKeyDown(KeyCode.Escape)) { visible = false; return; }
			if(Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
				activeTab = (byte)Math.Clamp(activeTab+(Input.GetKeyDown(KeyCode.UpArrow)?-1:1),0,sections.Length-1);
		}
		public static void ClampWindow()
		{
			windowPosition.x = Math.Clamp(windowPosition.x,4,Math.Max(4,Screen.width-WindowSize.x-4));
			windowPosition.y = Math.Clamp(windowPosition.y,4,Math.Max(4,Screen.height-WindowSize.y-4));
		}
		public void OnGUI()
		{
			if(!visible) return;
			ClampWindow();
			using var theme = Styles.UseTheme();
			float s = EffectiveScale, x = windowPosition.x, y = windowPosition.y;
			GUI.Box(new Rect(x,y,WindowSize.x,WindowSize.y),"",Styles.WindowBox);
			Styles.DrawBadge(new Rect(x+20*s,y+14*s,48*s,54*s));
			GUI.Label(new Rect(x+82*s,y+12*s,490*s,36*s),"AU-Anticheat",Styles.Title);
			GUI.Label(new Rect(x+84*s,y+49*s,560*s,28*s),"Protection and lobby controls   /   " + AUAnticheatPlugin.DisplayVersion,Styles.Hint);
			if(GUI.Button(new Rect(x+700*s,y+22*s,38*s,36*s),"X",Styles.SectionBox)) { visible = false; ExceptionsSection.Editing = false; }
			for(byte i=0;i<sections.Length;i++)
			{
				var rect = new Rect(SectionListPosition.x,SectionListPosition.y+i*54*s,SectionButtonSize.x,SectionButtonSize.y);
				if(GUI.Button(rect,sections[i].name,activeTab==i?Styles.SectionBoxActive:Styles.SectionBox))
				{ activeTab=i; LoadoutsEditor.Editing=ExceptionsSection.Editing=RulesEditor.Editing=false; }
			}
			GUI.Label(new Rect(x+22*s,y+524*s,165*s,56*s),"INSERT  Open / close\nDrag the top bar to move",Styles.Hint);
			GUI.Label(new Rect(x+22*s,y+577*s,220*s,23*s),"by xav0dev - Boo",Styles.Hint);
			RenderActiveSection(); // Navigation always renders before a fallible panel.
			HandleDrag();
		}
		[Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
		private void RenderActiveSection()
		{
			if(activeTab>=sections.Length) activeTab=0;
			var section=sections[activeTab];
			try
			{
				GUILayout.BeginArea(new Rect(FeaturePanePosition.x,FeaturePanePosition.y,FeaturePaneSize.x,FeaturePaneSize.y));
				try
				{
					section.scrollVector=GUILayout.BeginScrollView(section.scrollVector);
					try
					{
						GUILayout.Label(section.name,Styles.Title);
						if(sectionErrors[activeTab]==null) section.Render();
						else { Styles.Help("This panel could not be drawn. Other tabs still work."); if(GUILayout.Button("Retry panel")) sectionErrors[activeTab]=null; }
					}
					finally { GUILayout.EndScrollView(); }
				}
				finally { GUILayout.EndArea(); }
			}
			catch(Exception e) when(e.GetType().Name!="ExitGUIException")
			{ if(sectionErrors[activeTab]==null) AUAnticheatPlugin.Log.LogWarning($"Menu panel {section.name} failed: {e}"); sectionErrors[activeTab]=e.GetType().Name; }
		}
		private void HandleDrag()
		{
			var e=Event.current; if(e==null) return;
			float s=EffectiveScale;
			var header=new Rect(windowPosition.x,windowPosition.y,680*s,82*s);
			if(e.type==EventType.MouseDown && e.button==0 && header.Contains(e.mousePosition)) { dragging=true; dragOffset=e.mousePosition-windowPosition; e.Use(); }
			else if(e.type==EventType.MouseDrag && dragging) { windowPosition=e.mousePosition-dragOffset; ClampWindow(); e.Use(); }
			else if(e.type==EventType.MouseUp) dragging=false;
		}
	}
}
