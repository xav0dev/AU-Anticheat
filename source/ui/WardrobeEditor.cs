using AUAnticheat.anticheat;
using UnityEngine;
namespace AUAnticheat.ui
{
    internal static class WardrobeEditor
    {
        private static bool open;
        private static string deleting;
        public static void Render()
        {
            open=Styles.Foldout(open,"My saved outfits"); if(!open) return;
            Styles.Help("Save your own hat, skin, visor, pet, nameplate and color. Up to 24 named outfits on this device. Only unlocked inventory items can be applied; no copying other players or unlocking cosmetics.");
            InlineEditor.Field("Save current outfit as","",32,Wardrobe.Save);
            foreach(var item in WardrobeStore.Snapshot()) {
                GUILayout.BeginHorizontal();
                try {
                    if(GUILayout.Button("Wear: "+item.Name)) Wardrobe.Apply(item);
                    if(GUILayout.Button("Delete: "+item.Name)) deleting=item.Name;
                } finally { GUILayout.EndHorizontal(); }
                if(deleting==item.Name) {
                    if(GUILayout.Button("Confirm delete outfit: "+item.Name)) { WardrobeStore.Delete(item.Name); deleting=null; }
                    if(GUILayout.Button("Keep outfit")) deleting=null;
                }
            }
            Styles.Help(Wardrobe.Status); Styles.Help(WardrobeStore.Status);
        }
    }
}
