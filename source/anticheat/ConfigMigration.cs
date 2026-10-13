using System.IO;

namespace AUAnticheat.anticheat
{
    internal static class ConfigMigration
    {
        internal const string CurrentFileName = "com.au.anticheat.cfg";
        // Compatibility only: this literal locates existing user preferences.
        // Do not rename it or delete the user's original configuration.
        internal const string LegacyFileName = "com.xav.anticheat.cfg";

        internal static bool ImportIfMissing(string directory)
        {
            string current = Path.Combine(directory, CurrentFileName);
            string legacy = Path.Combine(directory, LegacyFileName);
            if(File.Exists(current) || !File.Exists(legacy)) return false;
            if(new FileInfo(legacy).Length > 65536)
                throw new IOException("Previous configuration exceeds the import limit.");
            File.Copy(legacy, current, overwrite: false);
            return true;
        }
    }
}
