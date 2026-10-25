# <img src="assets/au-anticheat-x.svg" width="32" height="36" alt="X badge"> AU-Anticheat — Beta 2

**by xav0dev - Boo** · **1.0.0-beta.2** · Windows / BepInEx IL2CPP

Defensive message checks, understandable alerts and host moderation for Among Us. Includes personal chat filtering for links and lobby promotions, saved rules, match summaries, entry-level exceptions, persistent local bans, optional community-list lookup and configurable lobby automation.

**Experimental release candidate:** automated checks passed; live multiplayer and x86 native startup still need testing. It cannot guarantee a cheat-free lobby, identify every sender, stop every removal attack or protect everyone's IP. Not affiliated with or endorsed by Innersloth.

**Important default:** referenced-owner punishment is ON, following the requested HostGuard-style approach. The referenced object's owner is not necessarily the sender. Forged actions can cause innocent players to be banned; persistent saving is also ON. Turn off **Detection → Use referenced-owner punishment (spoofable)** for observation-only attribution and review saved bans.

## Downloads

Available from [Releases](https://github.com/xav0dev/AU-Anticheat/releases) after the maintainer publishes this candidate:

| File | Purpose |
| --- | --- |
| `AU-Anticheat.dll` | Update an existing compatible installation. |
| `AU-Anticheat-1.0.0-beta.2-Install-x64.zip` | Fresh 64-bit installation: official BepInEx loader plus plugin. |
| `AU-Anticheat-1.0.0-beta.2-Install-x86-UNVERIFIED.zip` | 32-bit candidate, not validated against a 32-bit game or tested natively. |
| `AU-Anticheat-1.0.0-beta.2-Source.zip` | Corresponding plugin source, docs and optional service template. |
| `AU-Anticheat-1.0.0-beta.2-Tests.zip` | Developer tests and matching source; not for installation. |

Installation ZIPs contain **no tests, generated game libraries, player records or personal configuration**. Numeric DLL/loader version: `0.9.0.2`; displayed version: `1.0.0-beta.2`.

## Requirements

Compiled and metadata-checked against the installed **Among Us v19s / build 7489 x64** interop and **BepInEx 6.0.0-be.788+5b766a3**. Other updates/loaders are not guaranteed compatible. AnyCPU managed code does not by itself prove x86 native compatibility.

Choose by the **game executable's architecture**, not Windows architecture. Never mix x86/x64 loaders. This Windows plugin cannot be installed on Android, iOS or consoles. Other players do not need it for ordinary defensive-mode gameplay; your client does not control their clients.

## Installation

1. Close Among Us. Use a clean game folder or separate backup for beta testing.
2. Choose the matching installation ZIP. Extract its **contents** beside `Among Us.exe`, not into `BepInEx/plugins`. It contains official loader build 788 and `BepInEx/plugins/AU-Anticheat/AU-Anticheat.dll`.
3. Do not blindly overwrite a different loader installation. If compatible BepInEx already works, use the DLL-only update below.
4. Launch and allow first-run BepInEx generation/downloads to finish. First launch is not guaranteed to work offline.
5. Press **Insert**. Check the badge and `BepInEx/LogOutput.log`; inactive status/errors mean protection did not attach successfully.
6. Test normal gameplay with consenting players privately before using the beta publicly.

For manual installation use [official BepInEx downloads](https://builds.bepinex.dev/projects/bepinex_be) and the [Unity IL2CPP guide](https://docs.bepinex.dev/master/articles/user_guide/installation/unity_il2cpp.html). Choose `Unity.IL2CPP-win-x64` or `Unity.IL2CPP-win-x86`, **not Mono or BepInEx 5**, then put the DLL in `BepInEx/plugins/AU-Anticheat/`.

## Updating

1. Close the game and check release compatibility.
2. Back up the old DLL **outside** `BepInEx/plugins`.
3. Preserve `BepInEx/config/com.au.anticheat.cfg` and `BepInEx/config/AU-Anticheat/` for preferences, rules, lists and loadouts.
4. Replace the old DLL. Keep **one** active copy, including checking differently named older copies and subfolders.
5. Restart, review the settings, and test privately. Reinstall BepInEx only when release requirements call for it.

Nothing was automatically installed or published while preparing these files.

## Menu

**Insert** opens/closes; **Escape** closes. Change the key in **Settings → Choose menu key**. Drag the title bar. The transparent red theme and X status badge are retained.

- **Overview:** status and guidance.
- **Self:** saved outfits with preferred-color restoration, personal chat filters and automatic lobby return.
- **Lobby:** entry level rule with friend-code exceptions, editable auto-post rules, saved lobby settings, moderation and automation.
- **Lobby → Settings:** typed extended values, task counts/filters (including MedBay scan), device restrictions, sabotage and decontamination options for explicit modded host mode.
- **Alerts:** current-lobby history and optional technical detail.
- **Players:** game-color names, reported details, local lists and normal host moderation.
- **Protection:** supported packet, movement and removal guards.
- **Detection:** points, Alert / Kick / Ban / Persistent Ban response. Persistent Ban is the default anti-cheat response.
- **Settings:** key, appearance, persistence, reset and unload.

[DOCUMENTATION.md](DOCUMENTATION.md) explains every feature, Google setup and testing. [POLICY.md](POLICY.md) covers privacy/publication limitations; [LICENSES.md](LICENSES.md) lists bundled components.

## Troubleshooting

- **Blank panel/inactive:** check compatibility, duplicate plugins and startup errors; use the panel retry button. A badge is not a protection certification.
- **False detections:** disable owner punishment, review Alerts and preserve a redacted log. Lower thresholds increase false-removal risk.
- **No rules/results:** check host authority, readiness, 100-character cap and your own chat cooldown. Results wait five continuously ready seconds after returning.
- **Reports unavailable:** choose **Lobby → Local moderation and chat filters → Use AU-Anticheat review service**, then review/submit from Players. Existing blank endpoints remain disabled. Reports enter the private inbox as pending; only an explicit approved decision by its owner can publish a ban through the approval automation. Service quotas/availability can prevent delivery or synchronization; check its status.
- **Uninstall:** close the game and remove the plugin DLL. Keep configuration if wanted. Removing BepInEx also affects other mods.

For bugs include game/plugin versions, host/guest status and a normal-play reproduction. Redact friend/account IDs, IPs, lobby codes and private data. Never test attacks on public players.

## Credits and license

Based on **[Hydra by MrDiamond64](https://github.com/MrDiamond64/Hydra)** under **GPL-3.0**. Required upstream attribution and [LICENSE](LICENSE) remain. The fork excludes upstream trolling, device spoofing, sanction bypass and non-host forced-removal utilities from its build.

Selected classic gameplay controls were independently implemented after reviewing [AmongUsRevamped](https://github.com/ApeMV/AmongUsRevamped). Its GPL-3.0 DevicePatches informed targeted communications disruption near disabled consoles. This does not include its custom gamemodes or promise to block modified clients.

Targeted reviews of [BanMod](https://github.com/GiannBart/BanMod), [HostGuard](https://github.com/RaresHonour/HostGuard), Town of Host/Enhanced, BetterAmongUs, Impostor and other public material informed selected designs. BanMod informed summary timing/layout and host-mode separation; HostGuard-style ownership remains spoofable. This is not an audit of every menu or an endorsement; no private services/credentials or cheat binaries are bundled.

HostGuard's default-name word-pair detector is adapted under its MIT license, copyright (c) 2026 Rares; see [the retained notice](licenses/HostGuard-MIT.txt). Its three configured bot-name examples are bundled with optional name filtering. Matching these names is a moderation preference, not proof of cheating.

Installation packages include official **BepInEx** and dependencies under their respective licenses. Among Us and original assets belong to **Innersloth**. The plugin uses a loaded game font when available; its X badge is not an official certification mark.
