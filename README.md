# <img src="assets/au-anticheat-x.svg" width="32" height="36" alt="AU-Anticheat X badge"> AU-Anticheat — beta 1

**By xav0dev - Boo.** A defensive Among Us anti-cheat and lobby-moderation plugin for BepInEx IL2CPP.

AU-Anticheat checks supported game messages, provides local protection and alerts, and gives hosts normal moderation tools. It keeps normal gameplay in mind, including vents, meetings, role changes after death and delayed network updates.

**Experimental beta:** automated checks have passed, but live multiplayer testing is still required. It cannot guarantee a cheat-free lobby or protection against every removal attack. This project is not affiliated with or endorsed by Innersloth.

## Download

Look under [Releases](https://github.com/xav0dev/AU-Anticheat/releases) for the beta 1 download when available. Players need **AU-Anticheat.dll**, not the source or test folders. GitHub's automatic “Source code” downloads do not contain the compiled plugin.

Version: **1.0.0-beta.1**. The numeric loader/file version is **0.9.0.1**; that is expected.

## Requirements

- Among Us on Windows with a compatible x64 BepInEx IL2CPP installation.
- This build was compiled against Among Us **v19s / build 7489**, using BepInEx **6.0.0-be.788** interop libraries. Compatibility with other game or loader versions is not guaranteed.
- Other players do not need this plugin for ordinary gameplay. That does not mean your installation protects all of their clients.

The plugin is a Windows build. Checking messages from Android or console players does not make the DLL installable on those devices. Game and BepInEx libraries are not bundled.

## Installation

1. Close Among Us.
2. Download **BepInEx 6 Unity IL2CPP** from the [official BepInEx downloads](https://builds.bepinex.dev/projects/bepinex_be). This beta was compiled using **6.0.0-be.788**. Choose the Windows package matching your game executable: `Unity.IL2CPP-win-x64` for 64-bit or `Unity.IL2CPP-win-x86` for 32-bit. Do not choose Mono or BepInEx 5. This release targets the x64 setup listed above; other configurations are not verified.
3. Extract the BepInEx archive into the game folder containing `Among Us.exe`. On Steam, right-click Among Us, then select **Manage → Browse local files**. If a compatible BepInEx setup is already installed, skip extracting another copy. See the [official IL2CPP installation guide](https://docs.bepinex.dev/master/articles/user_guide/installation/unity_il2cpp.html) for help.
4. Launch the game once and let BepInEx generate its files. The first launch can take longer. Close the game afterward.
5. Download `AU-Anticheat.dll` from [Releases](https://github.com/xav0dev/AU-Anticheat/releases) and place it in the game's `BepInEx/plugins` folder.
6. Keep only one active copy of AU-Anticheat. Avoid other overlapping anti-cheat plugins.
7. Start the game and press **Insert** to open the menu. Check the status badge and `BepInEx/LogOutput.log` for startup errors.
8. Test normal gameplay with consenting players in a private lobby before relying on the beta.

Do not put source files, tests, reference projects or the entire source ZIP into the plugins folder. The AU-Anticheat download contains the plugin, not BepInEx or the game.

## Updating

1. Close Among Us and check that the new release supports your game and BepInEx versions.
2. Back up your current anti-cheat DLL **outside** `BepInEx/plugins`.
3. Back up and preserve `BepInEx/config/com.au.anticheat.cfg` and `BepInEx/config/AU-Anticheat/` to keep your settings and saved lists. Keep any older configuration files too; supported configuration migration runs automatically.
4. Download the new `AU-Anticheat.dll` from [Releases](https://github.com/xav0dev/AU-Anticheat/releases).
5. Replace the old DLL in `BepInEx/plugins`. Remove any duplicate or differently named older copies from that folder so only one version loads.
6. Launch the game, press **Insert**, and check your settings and startup log. Test in a private lobby before using the update publicly.

You do not need to reinstall BepInEx for every plugin update. Change it only when the release requirements call for a different compatible version.

## Using the menu

Press **Insert** to open or close it, **Escape** to close, or use its X button. Drag the top bar to move it and scroll inside the content panel.

- **Overview:** status and getting started.
- **Lobby:** entry levels, exceptions, saved rules, match summaries, loadouts and local chat moderation.
- **Alerts:** notification history and suspicious-action review.
- **Players:** color-coded player list, reported details and host moderation.
- **Protection:** covered packet, movement and removal safeguards.
- **Detection:** checks, evidence threshold and host response.
- **Settings:** saving, reset, appearance, session clearing and unload.

Read [Documentation.md](Documentation.md) for every feature, its settings and its limits.

## Troubleshooting and removal

- **INACTIVE or startup errors:** protections did not attach. Check game/loader compatibility and duplicate plugins. Do not assume the badge means all guards are working.
- **False detections or broken normal actions:** stop relying on that check, note the action and settings, and preserve the affected log. Restore your backed-up DLL if necessary.
- **Settings do not save:** check the in-menu save status and write access to the configuration folder.
- **Rules or results do not post:** check host authority, message length, lobby readiness and the shared chat cooldown. See the documentation before treating this as a failure.
- **Uninstall:** close the game and remove only `AU-Anticheat.dll` from `BepInEx/plugins`. Keep the configuration folder if you want your preferences for a later reinstall.

For a bug report, include game build, plugin version, host/non-host status and a short normal-gameplay reproduction. Redact personal information, friend/account IDs, IP addresses and lobby codes from logs or screenshots. Do not publish working crash/removal exploits or test them against public players.

## For developers

The repository contains the readable plugin source and separate regression-test folders. Tests are not built into the plugin and players do not need to install them. Build instructions, test scope and verification results are in [Documentation.md](Documentation.md#building-and-testing).

The assembly/display name is `AU-Anticheat`, the C# namespace is `AUAnticheat`, the entry point is `AUAnticheatPlugin`, and the plugin ID is `com.au.anticheat`. C# identifiers cannot contain hyphens. Historical names remain only in attribution, upgrade guidance, the previous-config lookup and rename-verification tests. Author credits are not project branding.

## Credits and license

AU-Anticheat is based on **[Hydra by MrDiamond64](https://github.com/MrDiamond64/Hydra)**. Hydra is licensed under **GPL-3.0**; this fork retains that license. The full license is in [LICENSE](LICENSE). Renaming the project does not remove its original attribution.

This fork preserves and hardens Hydra's anti-cheat and defensive message-validation work. Its build allowlist excludes upstream trolling, teleportation tools, role manipulation, immortality, device/version spoofing, sanction bypass and non-host forced-kick implementations. Defensive movement guards are distinct from teleportation cheat tools.

AU-Anticheat's additional work includes lobby-scoped evidence handling, message-rate checks, clearer authority boundaries, defensive moderation, local privacy controls, saved preferences and host-authorized lobby admission policies.

Public material from BanMod, Town of Host, Town of Host Enhanced, BetterAmongUs, Impostor, Grim and other projects informed targeted design reviews. Their implementations, assets, endpoints, private services and community ban lists were not bundled by those additions. The scope and source links are in [Documentation.md](Documentation.md#reference-review). No claim is made to have audited every menu or anti-cheat. An MIT notice on an unrelated component does not relicense this Hydra-derived fork.

Among Us belongs to Innersloth. AU-Anticheat is an independent community project, not an official Innersloth product.
