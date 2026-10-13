# AU-Anticheat — feature documentation

This guide describes **beta 1 (1.0.0-beta.1)** as a complete release. For installation, compatibility and credits, see [README.md](README.md).

## Contents

- [Authority and protection limits](#authority-and-protection-limits)
- [Overview and status badge](#overview-and-status-badge)
- [Lobby](#lobby)
- [Alerts](#alerts)
- [Players](#players)
- [Protection](#protection)
- [Detection and points](#detection-and-points)
- [Settings and appearance](#settings-and-appearance)
- [Privacy and saved files](#privacy-and-saved-files)
- [Building and testing](#building-and-testing)
- [Reference review](#reference-review)

## Authority and protection limits

**Host:** local protection plus normal host-authorized moderation, lobby entry rules, saved game settings and public chat features.

**Non-host:** covered messages received by your client can be rejected locally; local alerts, evidence review and chat hiding remain available. You cannot ban other players, take over the host or intercept messages sent only to other clients.

Blocking means preventing this installation from processing a covered message. It does not prove that every other player was protected or that an attacker was identified. A named object owner, claimed sender, repeated pattern, level, outfit or platform is not authenticated identity. Unknown senders must not be blamed on the player named in a forged message.

There is no guarantee of detecting every cheat, remote injection or hotkey. The plugin does not verify cosmetic purchases, detect all passive role viewers, hide everyone's IP addresses, stop network-level DDoS attacks, undo server removals or instantly reverse arbitrary actions for all players. The previously reported unidentified mass-removal incident has not been reproduced and confirmed fixed.

## Overview and status badge

Overview shows whether you are hosting and offers the main **Cheat detection** switch and a short menu guide.

- **ACTIVE:** protection hooks loaded and detection is enabled. Individual guards can still be off.
- **PAUSED:** detection is off; separately enabled guards may still run.
- **INACTIVE:** protection attachment failed. Check the log and compatibility.

The top-right red X badge uses the same status. It is not a guarantee that the lobby is cheat-free.

## Lobby

### Minimum entry level

Open **Lobby entry rule**, enable **Ban below minimum level**, and select a minimum of at least 1. Use the step buttons or **Choose exact minimum...**, enter the number and select **Apply minimum**. The default minimum is 10, but enforcement starts disabled.

The rule can be configured with players already present. It enforces a lobby preference using normal host bans, not cheating points or ErrorKick. The host/local player and saved friend-code exceptions are exempt.

Loaded, nonzero live level data takes priority over a stale join snapshot. If live data is unavailable, a non-conflicting current join record may be used. These are game-reported levels, not verified account levels.

Level 1 needs special care because the game's zero value can also mean data is still loading. Enforcement requires an observed level-one update, complete player data, both records showing zero, no earlier higher level for that player object, and **five stable seconds**. Waiting alone does not turn an unknown level into a real level 1. Missing evidence can therefore leave a genuine level-1 player unconfirmed.

The rule checks host authority, game ban permission and target/client mapping. It allows settling time, requests one removal at a time, waits for departure, then waits two seconds before another. An outstanding request that does not complete within fifteen seconds pauses admission enforcement instead of producing a burst. Changing settings cannot bypass a pending request. Entry-rule notices stay local and do not accuse the player of cheating.

### Friend-code exceptions

Open **Friend-code exceptions**, choose **Type friend code**, enter the complete code, then press Enter or **Add exception**. Close game chat before typing. Backspace edits; Escape stops typing. Use an entry's Remove button to delete it.

Up to 128 normalized, case-insensitive codes are saved locally. Exceptions bypass only the minimum-level rule: they do not exempt a player from detection or an explicit persistent ban. Adding an exception cannot undo a ban already sent. Friend codes are reported identifiers, not spoof-proof authentication.

### Saved lobby rules

Open **Saved lobby rules → Edit rules**. Type your rules, then select **Save rules**. Enter separates rules; Backspace edits. **Cancel editing** or Escape stops editing without saving. Close game chat before typing. You can also edit the saved text file and select **Reload saved rules**.

All rules must fit **one unlabeled message of at most 100 characters**, including separators. Up to eight short rule lines are accepted; blank lines and lines starting with `#` are ignored. The file limit is 4 KB. Invalid or overlong edits do not replace the previous rules; the plugin does not split them into multiple chat messages or silently truncate them.

Enable **Auto-post rules when lobby is full** and choose **1–120 seconds**. This uses the configured normal lobby capacity, from 4 to 15, rather than requiring 15/15.

The same initialized roster must remain full for the chosen delay. Rules post once for that stable full-lobby cycle. A departure or replacement cancels a queued post and restarts the delay; the next eligible refill can post again. An unchanged full roster gets no repeat. Toggling the option or reloading alone does not resend rules already posted to that roster. Game start, loss of host authority and disabling the option cancel unsent output.

### Match summaries

**Post results after each match** is enabled by default. As host, return to the same waiting lobby after a completed match. The plugin waits **five continuous seconds of player, lobby and chat readiness**, then waits for the shared chat cooldown.

It attempts one unlabeled message of at most 100 characters containing the winner, match duration and observed impostors with roles and kill counts. Names may be shortened, marked with `~`. Role keys include `I` = Impostor, `S` = Shapeshifter, `P` = Phantom and `V` = Viper; `k` means observed kills. Full details remain in local Alerts. If the summary cannot safely fit or required data is missing, it stays local rather than inventing names or sending multiple lines.

Kill counts use successful observed results, deduplicated per victim; failed/protected attempts are excluded. Saved round observations survive an impostor leaving before Play Again. They are not server-audited statistics. Starting another game, changing host/room, leaving or disabling the feature prevents stale reports. Current-round roles are not announced during gameplay.

### Shared detection notices and chat timing

**Share evidence-based detections in chat** permits host-only notices from the existing attributable threshold or confirmed-exploit paths, for example:

```text
[AU-Anticheat] Player got detected by anticheat for reason
```

The wording can say banned or kicked for the corresponding normal host removal request; that wording is not a separate server acknowledgment. Only ban/kick/detect notices get the label. Entry-level bans, unknown-sender incidents and mass-disconnect suspicions do not generate public accusations. Names and reasons are sanitized and shortened to fit.

Rules, summaries and these notices share one outgoing queue. Each send waits for at least **max(3 seconds, the game's chat send interval) + 0.25 seconds**. Your successful manual chat also postpones automation. Automatic sends consume the normal cooldown and can delay your next manual message. Clearing the queue or toggling options does not erase an outstanding cooldown.

Messages obey normal chat availability and restrictions. Automatic match-time chat requires a living host in an active meeting; lobby loading and start countdowns delay it. Notices expire after 20 seconds queued; rules/results after 90 seconds. Busy chat may mean a message is skipped. Duplicate pending rules/reports are coalesced, delayed local echoes are suppressed, and rejected sends are not retried in a burst. Local pop-ups do not send chat.

### Lobby extensions

**Accept offered lobby extensions** accepts the game's native extension only when offered. Requests are limited per offer and paced; status distinguishes requested, confirmed, expired and unconfirmed. Missing confirmation produces a warning, not forged activity or repeated requests.

This does **not** disable inactivity expiry, create extension offers, recreate the room or guarantee that players remain connected. Server approval is required.

### Lobby settings loadouts

While hosting a waiting lobby with no start countdown, open **Lobby settings loadouts → Name new loadout**, type a name and choose **Save current lobby settings**. Close chat before typing. Names allow 1–32 letters, numbers, spaces, hyphens and underscores; up to 20 loadouts are stored.

Loadouts store normal game options, including role options—not player identities or anti-cheat preferences. Apply requires confirmation and the **same game build, mode, map and player capacity**. Select those normally first. Applications are at least five seconds apart and use normal game synchronization. The plugin never evicts players to fit a preset. Check the visible settings before starting; a partially sent network update cannot always be rolled back after a failure.

Delete a preset with its Delete button and confirmation. Existing names are not overwritten automatically. Invalid or oversized saved files are refused without automatically replacing them. Resetting preferences preserves loadouts.

### Local moderation and chat filters

Open **Local moderation and chat filters**:

| Option | What it does |
| --- | --- |
| Hide lobby ads locally | Hides recognized recruitment phrases and invite links on your screen. Enabled by default; never bans. |
| Hide chat floods locally | Hides messages beyond five observed messages from the same player object in ten seconds. Enabled by default. |
| Use saved word filter | Hides chat matching your literal phrases. Disabled by default. |
| Hide chat from filtered names | Hides chat from matching reported names; does not change names or ban players. Disabled by default. |
| Enforce my persistent ban list when hosting | Uses your local friend-code ban list in hosted waiting lobbies, with normal paced bans. Enabled by default; initially empty. |
| Show possible AFK players in lobby | Local reminders after 60–600 seconds without observed movement/chat. Disabled by default; never automatically kicks. Lag and menus can look idle. |
| Guard local kick/ban and late-rejection requests | Blocks covered unauthorized/self-targeted requests from this installation and late startup rejection of already-ready clients. Enabled by default; retains ordinary host moderation and legitimate startup handling. |

Chat filters work locally in host and non-host lobbies. Other players may already have received the message. Filter matches and watchlists do not award cheating points, identify a remote menu or prove who sent an attack.

Under **Players**, select a player to add/remove a **local block** (hide their chat) or **local watch** (private review reminder). Hosts can confirm **Save persistent ban**. Matching current friend-code records are required. Persistent bans apply to your hosted waiting lobbies, not to the person's game account everywhere; level exceptions do not override them. The host is exempt.

Remove saved entries from the Lobby panel. Edit `filtered-words.txt` or `filtered-names.txt`, one literal phrase per line, then select **Reload moderation lists and filters**. `#` lines are comments. Each filter supports up to 128 phrases of 100 characters and 16 KB per file. The moderation list holds up to 512 entries. Invalid moderation files retain prior in-memory lists and prevent overwrite until corrected/reloaded. No external community ban list is imported.

## Alerts

Pop-up alerts normally last ten seconds. Accepted notifications remain in the current lobby's history after disappearing, up to 200 entries. History clears on leaving/changing lobby or **Clear alert history**; it is not a permanent cross-session archive. Pop-up queues and event intake are bounded to limit floods. **Show pop-up alerts** controls display; **Technical detail** switches between simpler text and diagnostic detail.

**Activity details** shows bounded join/message observations and recent incidents. Message counts concern the referenced player objects, not authenticated remote keyboard/menu input. Where known, alerts identify the affected target; otherwise they say unknown instead of guessing.

### Sender evidence

This review panel separates claimed actor, object owner, target and conflicting claims. Reported IDs and names can be forged. Blocking a malformed action and proving who sent it are separate decisions. **Clear sender evidence** also clears related-action analysis.

### Related suspicious actions

This is an observational foldout inside Alerts, not a separate menu tab or an automatic-ban detector. It groups existing flagged actions referring to the same lobby owner ID and spawned player object ID.

- At least three observations in at least two action families, spanning at least four seconds within a rolling 45-second window.
- At most one observation per family/object per two seconds; multiple checks on one message count once.
- Conflicting claims, missing player objects and shared-object targets are not used to blame a player.
- Normal round/meeting boundaries and existing vent, movement and ghost-role grace pause or clear relevant sequences.
- Evidence expires and resets with session/round/host changes, departure and object replacement; at most 256 observations and eight displayed groups.

The panel says **Sender UNKNOWN** because the referenced player could be a victim. Receipt order is not necessarily execution order. Correlation creates no points, bans, public messages, remote uploads or persistent identity records. Collection requires Cheat detection and Extra behaviour checks. No pattern does not mean no cheating.

## Players

The roster uses each player's visible in-game outfit color. Tap a name to scroll back to their details at the top. Available game-reported friend code, account ID, level and platform are displayed locally, not verified against a remote account service. Missing data stays unavailable; differing level records are labeled rather than silently borrowing another player's details.

Hosts can confirm **Kick selected player** or **Ban selected player**. Kick removes them from this lobby; Ban also prevents ordinary rejoining of that lobby. These are not global account bans. The host/local player is excluded. There is no deceptive timeout/ErrorKick punishment or non-host removal feature.

## Protection

| Guard | Purpose and boundaries |
| --- | --- |
| ErrorKick / ventilation guard | Filters covered suspicious ventilation sequences referencing players on this client. Requires Detection and UpdateSystem checks. Normal vents, vent cleaning and host moderation remain available. “ErrorKick” is not one uniquely identifiable network attack. |
| Check meeting results | Checks supported result messages for malformed counts, duplicate/invalid voters and related structure while retaining legal skip/dead-target values and supported role fields. Does not force outcomes or repair results processed elsewhere. |
| Filter invalid vote-kick requests | Filters covered malformed/replayed requests without disabling normal votes, vote-kicks or host bans. Claimed voters are not automatically punished. |
| Guard against unwanted teleports | Checks covered unexpected movement of your player with normal transition allowances. Cannot instantly restore everyone else's position or undo arbitrary actions. |
| Check Influencer image messages | Validates supported Influencer/SpiritGuide image messages before processing. Does not disable the legitimate roles. |
| Encrypted connection | Requests DTLS for your connection only. It is not IP masking, a VPN or protection against bandwidth-saturating attacks. |
| Lobby wall check (experimental) | Checks explicit lobby teleports against confidently traced walls/outer bounds and observes sustained positions beyond the loaded static-collision envelope. Does not snap back ordinary walking or ban solely for coordinates. Small wall-edge exploits may be missed. |
| Validate packets before use | Bounds supported message envelopes, nesting, lengths and payload reads before dispatch. |
| Block oversized messages | Rejects covered excessive message sizes to limit malformed-data handling. |
| Check message types | Validates supported game-data message types. Unknown semantics are not automatic proof of cheating. |
| Bound number decoding | Bounds packed-number parsing to limit malformed reads. |
| Guard voting-result memory | Bounds covered voting-result allocations. Does not prevent every crash or denial-of-service attack. |

Advanced safeguards show the blocked-packet count and latest reason. A blocked packet can be untrusted without having a verified sender. The local removal-request guard is also available under Lobby. None of these guards makes a genuine server removal reversible or protects traffic this client never receives.

## Detection and points

**Cheat detection** enables the detection layer; **Show detection alerts** controls its notifications; **Block invalid actions** rejects covered invalid messages before this client applies them. Turning blocking off can allow flagged actions through locally.

**Check platform inconsistencies** compares reported platform fields for contradictions. It does not scan a device, record hardware device IDs or prove which platform/menu someone actually uses.

**Extra behaviour checks** enables additional covered action-pattern checks and related-action review. The implementation checks observable behavior, not menu names or whether an Android/PC player installed a particular app.

Covered checks include supported role/state/target validation; vent, ladder and platform handling; task completion; kill/report/meeting requests; doors and sabotage; names, colors, levels and selected cosmetic/message constraints; scanner/animation replay; and action/message-rate patterns. Cosmetic values are not proof of ownership or purchase. Accepted server-authorized results must not be mistaken for unauthorized requests.

Movement observation compares sustained speed with expected movement and looks for repeated large jumps with tolerance for network catch-up. It excludes/resets around supported meetings, vents, ladders, platforms, role transitions and other normal movement contexts. A single correction or short burst is not enough to identify an attacker. Ordinary death/ejection ghost-role changes and supported Engineer/Influencer/SpiritGuide/Phantom/Shapeshifter behavior have dedicated allowances; this is not a guarantee against every false positive.

### Host response

- Minor attributable evidence: **50 points**; major attributable evidence: **100 points**.
- Default threshold: **100**, adjustable from **50 to 200**.
- **None:** records qualifying evidence without score-based removal.
- **Kick:** normal host removal at the threshold.
- **Ban:** normal host lobby ban at the threshold; the default response.

Confirmed exploit signatures can use a separate immediate-ban path, so selecting None is not a global disable-all-moderation switch. Unknown/unverified sender observations do not justify punitive points or a ban against the referenced player. Admission rules and manually saved bans are separate policies, not proof of cheating. Automatic removal requires host authority; the correlation panel never independently authorizes it.

**Individual message checks** exposes advanced RPC switches. Disabling one turns off that validation, not the player's ordinary ability. Use this for focused troubleshooting rather than assuming fewer alerts means better protection. Lower thresholds leave less room for mistakes.

## Settings and appearance

Preferences save automatically, checked about once per second, and also on normal shutdown/unload. This covers detection/guard switches, individual checks, response/threshold, lobby settings, alerts, size, opacity and window position. Check the menu's save status for failures. A crash can lose a just-made change. Open tabs, foldouts, typing drafts, pending confirmations and live evidence are temporary.

- **Reset all settings to defaults:** asks for confirmation, restores preferences and clears friend-code exceptions. Preserves written rules, loadouts, moderation lists and current alert history; does not undo bans. Minimum-level enforcement and auto-post rules return to disabled.
- **Menu size:** 0.75–1.5x, fitted to the screen.
- **Background opacity:** 35–100%, affecting menu, status badge and pop-ups; text/icons remain readable.
- **Reset appearance:** restores size, position, opacity and the fixed red theme. There is no accent-color selector.
- **Clear session evidence:** clears current scores and observation history, not saved exceptions or existing bans. Alert history has its own clear button.
- **Unload plugin:** confirmed action that stops its protections until the game restarts.

The interface uses the game's compatible loaded font when available, otherwise a readable fallback. Hover highlights borders rather than making the whole menu opaque. Branding uses a red X and the footer **by xav0dev - Boo**.

## Privacy and saved files

The default data directory is inside your game at `BepInEx/config/AU-Anticheat/`:

| File | Contents |
| --- | --- |
| `settings.json` | Saved anti-cheat/menu preferences. |
| `lobby-rules.txt` | Your written rules. |
| `lobby-loadouts.json` | Named normal-game option presets. |
| `moderation-list.json` | Your local friend-code ban/block/watch entries. |
| `filtered-words.txt`, `filtered-names.txt` | Your literal chat/name filter phrases. |

Friend-code exceptions also use `BepInEx/config/com.au.anticheat.cfg`. If this file does not yet exist, the first renamed startup imports the previous `com.xav.anticheat.cfg` without changing the original. An existing new configuration always wins; oversized old files are refused and import failures are logged. Other preferences remain in the unchanged `AU-Anticheat` data directory. These runtime files are not included in the source distribution. Keep local backups before reinstalling; reset is not a substitute for a backup.

The plugin does not add IP lookups, hardware-ID collection, remote process scans, automatic report uploads or third-party verification services. Session evidence/coordinates are bounded local observations. Diagnostic notifications can still be written to the ordinary **BepInEx logs**, including player names, event details and coordinates. Inspect and redact logs before sharing them.

Player details display identifiers already reported through the game. Saved moderation/exception lists intentionally retain friend codes locally. Opt-in/configurable public chat deliberately shares detection notices and completed-match observations with other players, who can record them. The project and menu identify the author as xav0dev - Boo; the project is not anonymous.

## Building and testing

Players install the DLL only. Developers use a .NET SDK capable of targeting .NET 6 and their own generated game/BepInEx libraries:

```powershell
$gamePath = 'C:/Program Files (x86)/Steam/steamapps/common/Among Us'
dotnet build source/AU-Anticheat.csproj -c Release "-p:GameDirectory=$gamePath"
dotnet run --project tests/ProtectionTests.csproj -c Release -- 'source/bin/Release/net6.0/AU-Anticheat.dll' "$gamePath/BepInEx/interop/Assembly-CSharp.dll"
dotnet run --project ui-tests/UiTests.csproj -c Release
dotnet run --project lobby-tests/LobbyTests.csproj -c Release
dotnet run --project role-tests/RoleTests.csproj -c Release
dotnet run --project movement-tests/MovementTests.csproj -c Release
```

Output: `source/bin/Release/net6.0/AU-Anticheat.dll`. `AU-Anticheat.sln` and `.csproj` files describe the build. Tests live beside `source/`, are not compiled into the plugin and are not installed in the game. Proprietary game libraries and excluded upstream cheat utilities are not distributed.

### Recorded beta verification

The 13 October 2026 renamed production Release build recorded zero warnings/errors and these passing assertion counts:

| Suite | Assertions | Scope |
| --- | ---: | --- |
| Protection/parser/metadata | 95,476 | Message structure, malformed-input corpus, bindings, related-action logic, branding and configuration migration. |
| UI | 441 | Menu behavior with managed Unity substitutes. |
| Lobby/chat/meeting | 613 | Admission, rules, summaries, cooldowns, moderation and meeting handling. |
| Roles/teleports | 105 | Supported role and movement-transition cases. |
| Movement/lobby boundary | 27 | Simulated geometry and movement safeguards. |

Some test substitutes emitted unused-field warnings. These results are a recorded check of the packaged code, not a claim that live multiplayer or every game update works. This beta 1 build uses AUAnticheat code identifiers and imports previous preferences when the renamed configuration does not yet exist. The author credit and required upstream attribution remain; detection behavior is unchanged by the rename.

Recorded DLL size: **224,768 bytes**. SHA-256:

```text
E9CD2E284A29CF14C4522FE1455EABCFF5AE5D97B9C6A495E4B66412A5766910
```

### Private-lobby checklist

Test with consenting players: joins and level loading; exceptions and paced bans; rules at different capacities and refills; manual chat near automatic messages; one post-match summary; save/restart/reset; loadout apply/delete; ordinary reports and emergency meetings; kills, doors and sabotage; Engineer vents and vent cleaning; shapeshift/revert; Influencer/SpiritGuide and ghost transitions; ladders/platforms; and delayed network updates. Confirm that ordinary play is not penalized and that non-host mode does not remove players.

Do not reproduce disruptive attacks against public players. Keep the exact error popup and a redacted log if something fails. Automated checks are regression tools, not a certification of universal protection.

## Reference review

These are historical design-review references, not bundled dependencies or endorsements. Selected public material was reviewed on 11–12 October 2026; no external cheat binary or private service was executed. The full original-fork credits and GPL notice are in [README.md](README.md#credits-and-license).

- [Hydra by MrDiamond64](https://github.com/MrDiamond64/Hydra): upstream source of this GPL-3.0 defensive fork.
- [BanMod](https://github.com/GiannBart/BanMod/tree/0dc37d20325289fb8efe30e866da522e78e5525b): targeted moderation/chat review and five-second summary-delay comparison; no private services imported.
- [Town of Host](https://github.com/tukasa0001/TownOfHost/blob/main/Patches/PlayerJoinAndLeftPatch.cs) and [its chat handling](https://github.com/tukasa0001/TownOfHost/blob/main/Patches/ChatCommandPatch.cs): timing/queue comparisons, not copied impersonation or chat-restriction bypasses. Current AU summaries use five seconds, not an older three-second setting.
- [Town of Host Enhanced](https://github.com/EnhancedNetwork/TownofHost-Enhanced/tree/3ab9cefd9a266245430701668ec3d355c2eb6057): selected admission, spam and protection concepts; custom-role assumptions and community ban services were not adopted.
- [BetterAmongUs](https://github.com/D1GQ/BetterAmongUs/tree/6c3df063881de3e33ac5740ad0b1f3090f5a7c4e/src/Patches/Gameplay/Anticheat): selected level initialization, removal pacing, voting, scanner and RPC checks. The separate archived BetterAmongUs-Public repository was metadata/reference discovery, not an imported implementation.
- [Impostor server ownership checks](https://github.com/Impostor/Impostor/blob/703915f06fcfd17bc8804d36dd67655671a66cf2/src/Impostor.Server/Net/Inner/InnerNetObject.Anticheat.cs) and [meeting-result serialization](https://github.com/Impostor/Impostor/blob/master/src/Impostor.Api/Net/Messages/Rpcs/Rpc23VotingComplete.cs): server-side authority and message-layout comparisons; client correlation cannot replace server authentication.
- [Grim violation configuration](https://github.com/GrimAnticheat/Grim/blob/2.0/common/src/main/resources/punishments/en.yml): bounded observation windows and separate alert/action concepts, not Minecraft rules copied into Among Us.
- [NuwaMenu sender fallback](https://github.com/extranig67/NuwaMenu/blob/432a94e4d8bfa31dadabdea7f77e6686b8e3d4f9/network/rpc_guard/NetworkGuard.RpcChecks.cs): recent-join association was rejected as a basis for automatic attribution.
- [wellsanticheat](https://github.com/somewhatpublicacc/wellsanticheat) and [AmongUS-AC-Engine](https://github.com/tatomodrekilaze/AmongUS-AC-Engine): scoped category/server-oriented comparisons, not evidence of universal official-server client protection.

EndlessHostRoles received discovery/overview review only. AmongUsRevamped, MalumMenu, TheOtherRoles/TheOtherHats, GameLogger and NLayer were not source-audited in this revision. NLayer is not required or bundled here. No claim is made to have researched every paid/private menu. Gameplay manipulation, synthetic server messages, remote reporting backends, ban bypasses and non-host forced removal were deliberately not imported.
