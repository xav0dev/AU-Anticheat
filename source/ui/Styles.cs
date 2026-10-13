using System;
using System.Collections.Generic;
using UnityEngine;

namespace AUAnticheat.ui
{
	// Code-drawn panels. Never modifies the game's shared GUI skin.
	internal static class Styles
	{
		public enum UIColors { Azure, Carbon, Cardinal, Pesto, Pumpkin, White, Violet }
		public static readonly Dictionary<UIColors, Color> ColorValues = new()
		{
			{ UIColors.Azure, new Color(.12f,.88f,.82f) }, { UIColors.Carbon, new Color(.38f,.48f,.56f) },
			{ UIColors.Cardinal, new Color(1,.35f,.4f) }, { UIColors.Pesto, new Color(.4f,.85f,.44f) },
			{ UIColors.Pumpkin, new Color(1,.68f,.25f) }, { UIColors.White, new Color(.9f,.95f,1) },
			{ UIColors.Violet, new Color(.72f,.55f,1) }
		};
		public static float menuOpacity = .75f;
		public static UIColors primaryColor = UIColors.Cardinal;
		public static Color Accent => ColorValues[primaryColor];
		private static readonly Color Ink = new(.035f,.055f,.085f);
		private static readonly Dictionary<string, Texture2D> textures = new();
		private static readonly Dictionary<string, GUIStyle> styles = new();
		private static GUISkin themedSkin;
		private static float builtScale, builtOpacity;
		private static UIColors builtColor;
		private static int scopes;
		private static Font builtFont;
		public static IDisposable UseTheme()
		{
			Ensure(); var previous = GUI.skin; GUI.skin = themedSkin; scopes++; return new SkinScope(previous);
		}
		private sealed class SkinScope : IDisposable
		{
			private readonly GUISkin previous;
			public SkinScope(GUISkin value) { previous = value; }
			public void Dispose() { GUI.skin = previous; scopes--; }
		}
		private static void Ensure()
		{
			float scale = MainUI.EffectiveScale;
			if(themedSkin != null && scopes > 0) return;
			Font gameFont = GameFont.Current;
			if(themedSkin != null && builtScale == scale && builtColor == primaryColor && builtOpacity == menuOpacity && builtFont == gameFont) return;
			ClearCache(); builtScale = scale; builtColor = primaryColor; builtOpacity = menuOpacity;
			builtFont = gameFont;
			themedSkin = UnityEngine.Object.Instantiate(GUI.skin);
			if(gameFont != null) themedSkin.font = gameFont;
			GUIStyle label = new(themedSkin.label) { fontSize = Size(15), wordWrap = true, richText = false,
				padding = Pad(3,3,5,5), margin = Pad(2,2,2,3) };
			label.normal.textColor = Color.white; themedSkin.label = label;
			if(gameFont != null) label.font = gameFont;
			GUIStyle button = new(themedSkin.button) { fontSize = Size(15), fontStyle = FontStyle.Bold,
				wordWrap = true, richText = false, padding = Pad(12,12,9,9), margin = Pad(2,2,4,4), border = new RectOffset { left=12,right=12,top=12,bottom=12 } };
			bool redTheme = primaryColor == UIColors.Cardinal;
			button.normal.background = Texture("button", redTheme ? new Color(.25f,.10f,.13f) : new Color(.15f,.22f,.28f), redTheme ? new Color(.72f,.40f,.44f) : new Color(.53f,.66f,.72f));
			button.normal.textColor = Color.white;
			if(gameFont != null) button.font = gameFont;
			button.hover.background = Texture("hover", redTheme ? new Color(.36f,.14f,.18f) : new Color(.2f,.32f,.39f), Accent); button.hover.textColor = Color.white;
			button.active.background = Texture("pressed", Accent, Color.white); button.active.textColor = Ink;
			button.onNormal = button.active; button.onHover = button.active; button.onActive = button.active;
			themedSkin.button = button;
			themedSkin.toggle = new GUIStyle(button) { alignment = TextAnchor.MiddleLeft };
			styles["panel"] = new GUIStyle(button) { normal = new GUIStyleState { background = Texture("panel", redTheme ? new Color(.16f,.055f,.075f) : new Color(.075f,.115f,.16f), Color.white, menuOpacity) } };
			// Keep the status/alert panel style unchanged. The interactive menu must
			// not inherit opaque button hover/active backgrounds from the skin.
			var fill = new Color(.16f,.055f,.075f);
			var window = new GUIStyle(styles["panel"]);
			window.normal = new GUIStyleState { background = Texture("window", fill, Color.white, menuOpacity) };
			window.hover = new GUIStyleState { background = Texture("window-hover", fill, Accent, menuOpacity) };
			window.active = window.focused = window.onNormal = window.onHover = window.onActive = window.onFocused = window.hover;
			styles["window"] = window;
			button.normal.background = Texture("menu-button", fill, new Color(.72f,.40f,.44f), menuOpacity);
			button.hover.background = Texture("menu-button-hover", fill, Accent, menuOpacity);
			button.active = button.focused = button.onNormal = button.onHover = button.onActive = button.onFocused = button.hover;
			themedSkin.button = button;
			themedSkin.toggle = new GUIStyle(button) { alignment = TextAnchor.MiddleLeft };
			styles["tab"] = new GUIStyle(button) { alignment = TextAnchor.MiddleLeft, fontSize = Size(16), fixedHeight = 0 };
			styles["selected"] = new GUIStyle(styles["tab"]) { normal = button.active };
			styles["title"] = new GUIStyle(label) { fontSize = Size(23), fontStyle = FontStyle.Bold };
			styles["heading"] = new GUIStyle(label) { fontSize = Size(17), fontStyle = FontStyle.Bold };
			styles["hint"] = new GUIStyle(label) { fontSize = Size(13) }; styles["hint"].normal.textColor = new Color(.67f,.77f,.84f);
			styles["danger"] = new GUIStyle(button); styles["danger"].normal.textColor = new Color(1,.53f,.52f);
		}
		private static int Size(int value) => Math.Max(1, (int)(value * MainUI.EffectiveScale));
		private static RectOffset Pad(int l,int r,int t,int b) => new() { left=Size(l),right=Size(r),top=Size(t),bottom=Size(b) };
		private static GUIStyle Get(string name) { Ensure(); return styles[name]; }
		public static GUIStyle MainBox => Get("panel");
		public static GUIStyle WindowBox => Get("window");
		public static GUIStyle SectionBox => Get("tab");
		public static GUIStyle SectionBoxActive => Get("selected");
		public static GUIStyle Title => Get("title");
		public static GUIStyle Heading => Get("heading");
		public static GUIStyle Hint => Get("hint");
		public static GUIStyle Danger => Get("danger");
		private static bool badgeUnavailable;
		public static void DrawBadge(Rect position)
		{
			if(badgeUnavailable) return;
			try
			{
				Ensure();
				if(!styles.TryGetValue("badge-draw",out var style))
				{
					style = new GUIStyle { border = new RectOffset(), padding = new RectOffset(), margin = new RectOffset() };
					style.normal.background = BadgeIcon;
					styles["badge-draw"] = style;
				}
				// GUI.DrawTexture forwards into a stripped overload in v19. GUI.Box
				// with a background texture uses the available native GUIStyle path.
				GUI.Box(position,"",style);
			}
			catch(Exception error) when(error.GetType().Name != "ExitGUIException")
			{
				badgeUnavailable = true;
				AUAnticheatPlugin.Log.LogWarning($"Decorative badge disabled; menu remains available: {error.GetType().Name}");
			}
		}
		public static GUIStyle PlayerName(Color color, bool selected)
		{
			Ensure();
			string key = $"player:{color.r}:{color.g}:{color.b}:{selected}";
			if(styles.TryGetValue(key,out var cached)) return cached;
			bool dark = .2126f*color.r + .7152f*color.g + .0722f*color.b < .35f;
			var style = new GUIStyle(SectionBox) { richText=false, fontStyle=selected?FontStyle.Bold:FontStyle.Normal };
			style.normal.textColor=style.hover.textColor=style.active.textColor=color;
			var background = Texture(dark ? "name-light" : "name-dark",dark?new Color(.91f,.94f,.96f):new Color(.06f,.09f,.12f),new Color(.5f,.61f,.66f));
			style.normal.background=style.hover.background=style.active.background=background;
			style.hover.background=style.active.background=Texture(dark ? "name-light-hover" : "name-dark-hover",dark?new Color(.91f,.94f,.96f):new Color(.06f,.09f,.12f),Accent);
			style.focused = style.hover;
			if(styles.Count<128) styles[key]=style;
			return style;
		}
		public static bool Toggle(bool value, string text) => GUILayout.Toggle(value, (value ? "ON   " : "OFF   ") + text);
		public static void Help(string text) => GUILayout.Label(text, Hint);
		public static bool Foldout(bool open, string text) => GUILayout.Button((open ? "-  " : "+  ") + text) ? !open : open;
		public static void HeadingText(string text) { GUILayout.Space(8 * MainUI.EffectiveScale); GUILayout.Label(text, Heading); }
		private static Texture2D Texture(string key, Color fill, Color border, float alpha = 1)
		{
			if(textures.TryGetValue(key, out var cached)) return cached;
			var texture = new Texture2D(32,32) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
			for(int y=0;y<32;y++) for(int x=0;x<32;x++)
			{
				bool outer = Rounded(x,y,1,30,8), inner = Rounded(x,y,4,27,6);
				Color c = !outer ? Color.clear : inner ? fill : border; c.a *= alpha; texture.SetPixel(x,y,c);
			}
			texture.Apply(); textures[key] = texture; return texture;
		}
		private static bool Rounded(int x,int y,int lo,int hi,int radius)
		{
			if(x<lo || y<lo || x>hi || y>hi) return false;
			int dx = Math.Max(lo+radius-x, Math.Max(0,x-(hi-radius)));
			int dy = Math.Max(lo+radius-y, Math.Max(0,y-(hi-radius)));
			return dx*dx+dy*dy <= radius*radius;
		}
		public static Texture2D BadgeIcon
		{
			get
			{
				Ensure(); if(textures.TryGetValue("badge", out var icon)) return icon;
				icon = new Texture2D(64,72) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
				for(int y=0;y<72;y++) for(int x=0;x<64;x++)
				{
					// A bordered X with transparent surroundings, drawn without GUI.DrawTexture.
					int diagonal = Math.Min(Math.Abs((x-32)-(y-36)),Math.Abs((x-32)+(y-36)));
					bool outer = x>=8 && x<=56 && y>=12 && y<=60 && diagonal<=8;
					bool inner = x>=11 && x<=53 && y>=15 && y<=57 && diagonal<=4;
					Color c = inner ? Accent : outer ? Color.white : Color.clear;
					icon.SetPixel(x,y,c);
				}
				icon.Apply(); textures["badge"] = icon; return icon;
			}
		}
		public static void ClearCache()
		{
			foreach(var texture in textures.Values) UnityEngine.Object.Destroy(texture);
			textures.Clear(); styles.Clear();
			if(themedSkin != null) UnityEngine.Object.Destroy(themedSkin);
			themedSkin = null;
		}
	}
}
