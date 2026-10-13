using System;
namespace AUAnticheat.anticheat
{
    internal static class SceneBoundaryRules
    {
        public static bool Usable(float minX,float minY,float maxX,float maxY,int count) =>
            count>=2 && float.IsFinite(minX) && float.IsFinite(minY) && float.IsFinite(maxX) && float.IsFinite(maxY) &&
            maxX-minX>=3 && maxY-minY>=2 && maxX-minX<1000 && maxY-minY<1000;
        // Intentionally generous. This is an outer envelope, not a navigation mesh.
        public static bool Outside(float x,float y,float minX,float minY,float maxX,float maxY) =>
            float.IsFinite(x) && float.IsFinite(y) && (x<minX-2 || x>maxX+2 || y<minY-2 || y>maxY+2);
        public static bool Sustained(int samples,float seconds) => samples>=4 && float.IsFinite(seconds) && seconds>=2;
    }
}
