using AUAnticheat.anticheat;

internal static class ConfigMigrationTests
{
    internal static void Run(Action<bool, string> check)
    {
        string directory = Path.Combine(Path.GetTempPath(), "AU-Anticheat-config-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string oldPath = Path.Combine(directory, ConfigMigration.LegacyFileName);
        string newPath = Path.Combine(directory, ConfigMigration.CurrentFileName);
        try
        {
            check(!ConfigMigration.ImportIfMissing(directory), "fresh install has no legacy import");
            const string preferences = "[Admission]\nFriendCodeExceptions = example#1234\n[LobbyRules]\nEnabled = true\nFullLobbySeconds = 20\n";
            File.WriteAllText(oldPath, preferences);
            check(ConfigMigration.ImportIfMissing(directory), "previous preferences imported on first renamed startup");
            check(File.ReadAllText(newPath) == preferences, "exception and rule preferences preserved byte for byte");
            check(File.ReadAllText(oldPath) == preferences, "original config is retained");
            File.WriteAllText(newPath, "user's new preferences");
            check(!ConfigMigration.ImportIfMissing(directory) && File.ReadAllText(newPath) == "user's new preferences", "existing new preferences never overwritten");
            File.WriteAllText(newPath, "");
            check(!ConfigMigration.ImportIfMissing(directory), "intentionally empty new config is not overwritten");
            File.Delete(newPath);
            File.WriteAllText(oldPath, new string('x', 65537));
            bool rejected = false;
            try { ConfigMigration.ImportIfMissing(directory); } catch(IOException) { rejected = true; }
            check(rejected && !File.Exists(newPath), "oversized old config is rejected without destination");
        }
        finally
        {
            // Only explicitly named fixtures in this test's freshly created folder.
            if(File.Exists(oldPath)) File.Delete(oldPath);
            if(File.Exists(newPath)) File.Delete(newPath);
            Directory.Delete(directory);
        }
    }
}
