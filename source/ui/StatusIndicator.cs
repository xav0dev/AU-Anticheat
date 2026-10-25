using AUAnticheat.anticheat;
using UnityEngine;

namespace AUAnticheat.ui
{
	internal sealed class StatusIndicator : MonoBehaviour
	{
		private GUIStyle cachedHeading, title, status;
		private int cachedFontSize;
		public static string StateLabel => !AUAnticheatPlugin.ProtectionsAttached ? "INACTIVE" : Anticheat.Enabled ? "ACTIVE" : "PAUSED";
		public void OnGUI()
		{
			using var theme = Styles.UseTheme();
			float s = Mathf.Clamp(Screen.height/1080f,.65f,1.15f);
			float w=250*s,h=36*s,x=Screen.width-w-5,y=4;
			GUI.Box(new Rect(x,y,w,h),"",Styles.MainBox);
			Styles.DrawBadge(new Rect(x+7*s,y+3*s,27*s,30*s));
			int fontSize=(int)(14*s);
			if(!ReferenceEquals(cachedHeading,Styles.Heading) || cachedFontSize!=fontSize)
			{
				cachedHeading=Styles.Heading; cachedFontSize=fontSize;
				title=new GUIStyle(cachedHeading) { fontSize=fontSize,wordWrap=false };
				status=new GUIStyle(Styles.Hint) { fontSize=(int)(11*s),fontStyle=FontStyle.Bold,wordWrap=false };
			}
			status.normal.textColor = !AUAnticheatPlugin.ProtectionsAttached ? new Color(1,.4f,.4f) : Anticheat.Enabled ? Styles.Accent : new Color(1,.75f,.3f);
			GUI.Label(new Rect(x+40*s,y+5*s,131*s,28*s),"AU-Anticheat",title);
			GUI.Label(new Rect(x+175*s,y+7*s,73*s,25*s),StateLabel,status);
		}
	}
}
