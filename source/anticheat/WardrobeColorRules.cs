using System.Collections.Generic;
namespace AUAnticheat.anticheat
{
    internal static class WardrobeColorRules
    {
        // Palette order: preceding chip, wrapping to the last chip at the left edge.
        public static int Select(int preferred,int count,ISet<int> occupied)
        {
            if(preferred<0 || preferred>=count || count>256) return -1;
            for(int offset=0;offset<count;offset++)
            { int candidate=(preferred-offset+count)%count; if(!occupied.Contains(candidate)) return candidate; }
            return -1;
        }
    }
}
