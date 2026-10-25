# AU-Anticheat — Beta 2 documentation

**1.0.0-beta.2**, numeric DLL **0.9.0.2**, prepared **25 October 2026**. Installation and updating are separate in [README.md](README.md).

## Authority and limits

This is a client plugin, not the official server. Hosts can use ordinary moderation and supported host options. Guests can filter covered incoming actions **locally**, observe and alert, but cannot reliably protect every client or remove arbitrary players.

Object owners, payload IDs, friend codes, names and platforms are not independently authenticated packet senders. Referenced-owner punishment is ON by request and can punish a spoofed victim. Turning it OFF is the safer observation-only policy. Sequence correlation does not turn a guess into verified identity.

No guarantee covers every kick, ban, ErrorKick, injection or disconnect. The plugin cannot reverse arbitrary server/remote-client actions, inspect other devices' processes, verify cosmetic purchases, or shield everyone's IP. DTLS is connection encryption, not a VPN or DDoS protection.

## Beta 2 changes

| Request | Result and limits |
| --- | --- |
| Opening button cooldown | Uses live `ShipStatus.EmergencyCooldown`, not the configured post-meeting duration; 2.5-second uncertainty tolerance. Early/transition button observations assign no points. |
| Outside-map/lobby detection | Tighter outer envelope, sustained samples and four inset wall probes; settling/vent/meeting/animation exclusions. Coordinates alone never convict. |
| BanMod-style summaries | One multiline winner/kills/tasks/time bubble, five ready seconds after returning plus shared chat cooldown. Shortens names or uses compact text to fit 100 characters. |
| Meeting spam | Above eight report requests per referenced object in five seconds, ignore locally; no punishment of the claimed author. |
| Bot/default names | Bundled HostGuard bot names and default-name word-pair detector (enabled with Ban selected by default), plus custom exact-name files. Matching is moderation, not bot identification. |
| Join floods / lock | Default eight joins/ten seconds after initial ten-second roster grace. Pauses auto-start 30 seconds and requests private lobby; no bans or automatic reopening. |
| Persistent anti-cheat bans | Persistent Ban is the default response, alongside Alert, Kick and Ban. Saves a loaded matching friend code locally after a ban request. Spoofable ownership and save failures remain possible. |
| Chat/name actions | Hide/alert, Kick, Ban, PersistentBan; hide/alert default. Shared pacing and execution-time revalidation. Ads have a separate optional host response; private blocks never trigger bans. |
| Auto-start | Off by default, full-lobby target. Optional player target and 5–120-second stable-ready-roster delay; ordinary start checks/countdown. |
| Automatic return | Off by default. Normal Play Again once after eight seconds for the same ended game/host; never reconnects after kick/ban. |
| Shared/online list | Optional Google Sheets CSV; lookup off, Ban response selected by default. Bounded validation, expiry and paced host enforcement. Claims are not verified evidence. |
| Online reports | Private review inbox and explicit preview/submission. The owner's approved Sheet decisions can publish a shared-list ban through the separately installed approval trigger; submissions alone never create bans. |
| Proximity | Optional observation, default off and Alert response. Optional host Kick/Ban/Persistent Ban applies to both players (host exempt); no points, online reports or role exposure. Normal cooperation can trigger it. |
| Extended settings | Offline-selected host-modded mode, typed numeric values, custom preset and serialized-value checks before sync. Normal mode remains default; cross-client behavior unverified. |
| Saved outfits | Up to 24 named outfits from your unlocked inventory. Save/wear outside a round/countdown; five-second apply spacing. |
| Menu key | Insert/Home/F1–F12; saved and resettable. |
| Packaging | Separate installers, DLL, source and tests; x86 unverified; no tests in installers. |
| Role encryption | **Not implemented as a false security claim.** A recipient that must interpret a role can expose it to its plugins. A different cooperating-client/server protocol would be separate work. |

## Menu, players and alerts

Nine navigation entries: Overview, Self, Lobby, an indented Lobby Settings page, Alerts, Players, Protection, Detection and application Settings. Personal outfits, chat filters and automatic lobby return are under Self. Red transparent theme, X status badge and `by xav0dev - Boo` remain. Hover changes borders without making the window opaque. Uses the game's source font when available, otherwise a fallback.

Player names use reported in-game colors. Select a player for reported account/friend-code, level/platform details and available moderation. These fields can be delayed, missing or spoofed. Local watchlists are review reminders, not evidence.

Popups expire (default ten seconds); up to 200 accepted notifications stay in current-lobby memory until cleared/session changes. Technical details and related-action histories are optional. Clearing evidence does not undo bans or erase existing BepInEx logs. Unknown affected players/senders are not invented.

## Entry levels and persistent lists

Configure the minimum under **Lobby → Lobby entry level rule**. It is an admission preference, not cheat evidence. Host exempt. Friend-code exceptions are inside that rule and apply to level policy, not an explicit saved ban.

The current live level takes precedence over stale join snapshots. Incomplete/conflicting data waits. Displayed level 1 (raw zero) requires an observed `SetLevel(0)`, complete matching identity and no higher level on the same object, followed by **five seconds** settling. A timeout alone is not proof. A true level-one player whose update was missed can remain pending; this avoids banning higher-level players during initialization glitches.

Level/saved-ban/filter/list moderation shares one removal pacer. Wait for departure then cooldown; unconfirmed removals pause instead of creating a retry flood. Enabling in a full lobby never intentionally batch-bans. Server acceptance is not guaranteed. Entry bans produce local policy notices, not public hacker accusations.

`moderation-list.json` stores local `ban`, `block`, `watch` entries, initially empty. Enforcement and automatic anti-cheat-ban saving default ON. These are local admission bans, not official account sanctions. Review/remove mistakes in **Lobby → Local moderation and chat filters**. Friend codes are game-reported identifiers, not verified hardware/account identities.

## Rules, results and public chat

Open **Lobby → Auto-post lobby rules** to edit rules in-menu or in `lobby-rules.txt`, save, then enable auto-post. ONE message, at most **100 characters**, no split flood or silent truncation. Whenever a lobby of its configured capacity fills and the same full roster remains for the chosen delay, rules queue once. A departure/replacement restarts the timer; unchanged full lobbies do not repost.

Results preserve observed impostor roles/names after departure. Successful observed kills are deduplicated. Missing information is never invented; if no impostor data exists the incomplete result stays local.

```text
Impostors Win
First: 1 kill
Second: 3 kills
Task: 10/45
Match Time: 03:51
```

Names shorten with `~`; exceptional rosters use compact I/S/P/V role abbreviations and `k` for kills, or remain local if they cannot fit. Full observed details remain in Alerts. Task totals appear only if observed. These are not server-certified statistics.

Rules/results have **no project label**. Evidence-based ban/kick/detection notices carry AU-Anticheat branding and disclose unverified owner attribution. Unattributed/entry-policy events do not become public hacker accusations.

All automatic messages use one queue and native chat availability/cooldown, **including manual sends**, plus a safety margin. Results require five continuously ready seconds after returning. Duplicate echoes do not repeat network sends. Chat restrictions are not bypassed.

## Extensions and saved lobby settings

**Accept offered lobby extensions** only acts on valid server/game offers and waits for confirmation. It cannot disable the inactivity countdown if no extension is offered.

**Lobby → Saved lobby settings** stores named game-option presets locally. Click the name field to type a name and press Enter, just like saving outfits. Apply only as host in a waiting lobby, not during countdown. Build/mode/map/capacity must match and game validation remains. Never evicts players to fit a preset. Maximum 20 names of 1–32 letters/numbers/spaces/hyphens/underscores. Corrupt files are not overwritten silently. Extended native values may be restored only in explicitly selected modded mode and within supported bounds. Device/sabotage/task-filter rule drafts save separately in preferences, not inside named game-option presets; apply those drafts separately.

## Chat/name/AFK moderation

Ads and floods are hidden **on this client**. Official-server routing can deliver them elsewhere first; not network-wide censorship. Recruitment detection is heuristic: links (including common dot-obfuscation), Discord invites, six-letter codes and short SNS/Expert/mod-lobby promotions. Ordinary discussion and malformed Unicode have regression coverage, but no heuristic recognizes every language or avoids every false positive. Alert hides the message here without removal; it does not retract messages already relayed to other clients. Word/name filters use literal phrases, not executable regexes. Custom bot/default names use whole normalized matches. Bundled default names use HostGuard's strict generated-name casing and word pairs.

Edit `filtered-words.txt`, `filtered-names.txt`, `bot-names.txt`, `default-names.txt`, one entry/line, `#` for comments. Maximum 128 entries/16 KiB each. Reload using the menu. Every installation includes HostGuard's TNT, auser and Haunt Bot examples and its 2,401/2,942 default-name word sets. Both name filters default ON with Ban selected, and can be disabled independently. Existing saved preferences are preserved. No name is proof that a player uses a menu.

Chat/name responses default hide/alert. Host may choose kick/ban/persistent ban, separate from cheat points and based on spoofable ownership. Queued actions recheck settings, binding and applicable name/list eligibility. Ads have their own Alert/Kick/Ban/Persistent Ban choice, default Alert. Private blocks never cause removals.

Optional AFK observations after 60–600 seconds without observed movement/chat. Alert is the default; host Kick/Ban/Persistent Ban can be selected separately. Activity and player binding are rechecked immediately before queued removals. Lag/menu use can appear idle.

## Join protection and automation

Private locking removes public listing, not code/invite access, and preserves existing players. Initial roster/Play Again grace avoids ordinary-sync false floods. It cannot stop every server-side raid.

Auto-start waits for ready, complete player objects and a stable roster. The target defaults to full capacity; custom count is clamped to game minimum/capacity. Uses ordinary countdown. Auto-return uses normal results navigation once. Both default OFF. Server closure, bans and disconnects are not overridden.

## Extended settings — opt-in modded mode

Leave the server, open **Lobby → Settings → Extended settings**, select modded mode, then host a NEW lobby. It registers Innersloth's documented host-only `+25` mode. Connected normal lobbies are not silently converted. Do not combine with another version-registration mod.

Ranges: speed/vision 0.25–5; kill/emergency/role cooldowns 0–180 seconds; discussion/voting 0–600; Engineer vent time and Shapeshifter/Phantom/Tracker durations 1–180; Scientist battery 1–180; protection duration 1–60; Tracker delay 0.25–60. Voting zero retains the game's unlimited meaning. No arbitrary packet injection or >15-player support.

Read current values, click a value to type it, then Save value (or Enter). Decimal values accept dot or comma; meeting times require whole numbers. Values outside the displayed range are rejected without replacing the draft. Escape cancels. This replaces extended-setting sliders and preserves float precision. Click Apply while hosting a waiting modded lobby. The code selects the Custom preset and checks values after serialization before ordinary sync, with five-second spacing. Failed synchronization attempts rollback; partial remote updates may still occur. Verify with other players before starting; live cross-client testing remains outstanding.

Additional native controls include common/short/long task counts (0–10 / 0–30 / 0–15), impostor and emergency counts, kill distance, task-bar mode, visual tasks, confirm ejects, anonymous votes and supported role options. A map may not contain enough distinct tasks for the requested count; verify the assigned tasks in private play.

**Devices, sabotages, meetings and tasks:** opt-in camera/vitals/admin/door-log restrictions; allowed sabotage types and ghost sabotage; initial emergency delay; map-specific sabotage duration/cooldown; MIRA/Polus decontamination timings; same assigned tasks; and a reduced crew task-completion threshold. Defaults retain normal behavior. These are selected classic AUR-style controls, not a port of every AUR setting or gamemode. They apply only to the current modded host lobby after Apply. They never award cheat points for the configured rule.

Device restrictions block console use for the host and send temporary targeted communications disruption near discovered matching consoles for guests. This can hide tasks or disable other comms-dependent abilities while nearby. Moving away, venting or meetings clears that temporary state; a real comms sabotage always takes priority and is never repaired by the rule. Unknown console/comms types remain usable. Scientist portable vitals and Fungle binoculars are not covered. Modified clients can ignore this gameplay restriction, so it is not an anti-cheat barrier.

**Disable selected task types → Disable MedBay scan (prevent scan clears)** removes SubmitScan from the host's task-assignment pool before the game. Other task types can also be excluded. Leave at least one task of every requested length available: if a filter would empty a pool, that pool is left unchanged and a status message explains why. Already assigned tasks are not retroactively removed. These options require a private cross-client test before public use.

## Your saved wardrobe

Open **Self → My saved outfits**, name your current outfit, and save it. Up to 24 unique names (1–32 characters) are stored in `wardrobe.json`, including hat, skin, visor, pet, nameplate and color. Wear or delete a selected preset; deletion requires confirmation. Existing presets are not silently overwritten. Invalid files are preserved rather than replaced.

Only your own unlocked inventory is accepted, checked again when wearing a preset. Save/wear at the main menu or in a waiting lobby, never during an active round or start countdown. Applying uses the game's ordinary cosmetic requests, at most once every five seconds. Saves the selected wardrobe color. When occupied, tries preceding palette IDs to the left, wrapping at the edge, then asks the host using the normal color request. The optional restore switch checks every five seconds in waiting lobbies until the saved color is available. Manual color selection cancels the previous preference. Host decisions and races with other players can still change the final color. Nothing unlocks paid items or copies another player's outfit. Native inventory and multiplayer testing remain outstanding.

Drafts persist, but mode selection resets each launch. Reset preferences does not silently change an active connection's mode.

## Detection, evidence and protections

Minor 50 points, major 100. Default threshold 100, configurable 50–200. Host response buttons are Alert, Kick, Ban, Persistent Ban; Persistent Ban is the default. The same chosen response also applies to immediate exploit-pattern detections. Alert does not remove the player; other independent admission/filter policies retain their separate response choices. Ban is lobby-only; Persistent Ban also attempts to save the matching friend code locally. Older Ban plus automatic-saving preferences migrate to Persistent Ban.

Referenced-owner punishment defaults ON by request. Eligible PlayerControl RPC context may use this heuristic; shared-object, chat/identity, movement and platform observations remain excluded. No sequence algorithm here authenticates every original sender on official servers. Safer review-only mode: owner punishment OFF. Persistent saving records a matching friend code after a ban request, not proof the server removed a cheater.

Supported checks cover RPC/game-data structure/length, bounded decoding/nesting, voting allocation/results, task/role/scan actions, doors/sabotage, ventilation sequences, vote-kick replay/structure and local removal authority. Influencer/SpiritGuide image messages are bounded before supported image handling. Supported normal ghost-role changes, Engineer vents, vent cleaning, shapeshift, ladders/platforms and meeting transitions have allowances. No claim to detect every menu or local-only ESP.

Movement requires stable living-player context. Lobby explicit snaps can be rejected locally against fresh unambiguous collision evidence. Sustained outer-bound/deep-wall observations are review-only, never automatic global snap-back. Four inset wall probes avoid ordinary brushing; geometry can still be incomplete.

Optional proximity observes two stable living players within 1.5 units for 30 seconds, each moving at least five units. Gaps/transitions reset tracking; alerts are throttled. Cooperation is normal: no points, online reports or role exposure. Under **Lobby → Local moderation and chat filters**, Alert is the default; optional Kick/Ban/Persistent Ban applies to BOTH matched players, excluding the host. Leaving proximity, death, stale samples or a changed lobby cancels queued eligibility.

Local removal guards reject covered unauthorized/self-targeted kick/ban calls and inappropriate late rejection of ready clients. They do not cancel server decisions or stop every remote attack. Normal host moderation and legitimate vote-kicks remain. **No deceptive ErrorKick punishment is added.**

## Google Sheets community list

Off by default. Enabling downloads the supplied [public sheet](https://docs.google.com/spreadsheets/d/1X6mvEPqhUeBf-mEluQJ4S0BbBkwVGVMIeYUNCCTIny0/edit?gid=0). Google sees your connection IP; no player records are uploaded for lookup.

Four required columns: `friend_code,status,reason,expires_utc`. Status is `ban` or `watch`; expiry is blank or ISO UTC ending in `Z`. Headers were prepared separately; no bans were seeded. Header-only lists work. CSV access must be public.

Downloads limited to once/15 minutes, 256 KiB and bounded rows/cells, with Google HTTPS destination checks. Malformed content fails closed; expired/invalid entries are ignored and conflicting duplicates excluded. Snapshots older than 24 hours are not enforced.

Ban response selected by default; downloading remains opt-in. Existing saved choices are preserved. Watch entries never trigger automatic removal. Explicitly selected host response may enforce ban entries through paced moderation with matching game-reported friend codes. List claims are not verified cheating evidence. Do not publish allegations as established facts.

## Optional online reports

The maintainer's Google Apps Script endpoint is configured as the default for new installations. On 24 October 2026 the production HTTP adapter downloaded the public CSV and received acceptance for one synthetic report. Its pending row was verified in the separate private inbox, then labeled `SELF-TEST ONLY` / `test-only`. No real player was reported and no public ban was added. The public list was valid and empty at verification time.

Open **Lobby → Local moderation and chat filters** to change the Reporting service URL. Existing blank settings remain disabled; choose **Use AU-Anticheat review service** to configure it. Clearing the URL disables reporting. Merely configuring the URL sends nothing. The service may become unavailable or reach its quota; receipt confirmation is checked for every submission.

Maintainer setup, if you choose to operate it:

1. Create an Apps Script project with `services/ReviewReports.gs` if operating your own service.
2. Run `setupReviewInbox` yourself and review Google's authorization prompts. It creates a **separate private spreadsheet**, a `Review` tab and the five required headers. It stores the workbook ID as `REVIEW_SHEET_ID` in Script Properties. Running setup again preserves the existing inbox.
3. Keep that workbook private. Do not use a tab inside the public ban-list workbook. Never publish the private workbook ID, credentials or report records in your release.
4. Review Google's permissions and deployment prompts yourself. Client submissions require a web-app endpoint reachable without interactive sign-in; the plugin does not carry your Google credentials. A public endpoint is an abuse surface. The template bounds body size, uses a lock, caps 100 accepted reports/day and 5,000 rows, and temporarily suppresses duplicate codes. Not authenticated reporting or DDoS protection.
5. Deploy as a Web app running as the owner with access **Anyone**, without requiring Google sign-in. Then set the Reporting service URL in-menu to the HTTPS `https://script.google.com/macros/s/.../exec` address. A deployment restricted to “Only myself” returns a sign-in page to the game and cannot receive reports from it.
6. For approval automation, set Script Property `PUBLIC_BAN_SHEET_ID` to your public list workbook (the template defaults to AU-Anticheat's configured list), then run `installApprovalAutomation` once as its owner. Review any new Google authorization request. It validates both workbooks, adds private feedback columns F/G, and installs one edit trigger without processing old decisions. Keep the report workbook private; anyone you grant edit access can make approval decisions.

In-game select a player → **Review online report** → inspect payload/destination → explicitly submit. Sends selected friend code, fixed unverified-suspicion claim and version. No IP/device IDs/chat logs in the body; Google/service networking still observes connection IPs. One/minute, no automatic POST retry. Supported Google content replies are fetched via GET; only explicit JSON acceptance counts as receipt.

The submission endpoint marks every report pending, ignores arbitrary reasons/formulas, and never exposes the inbox through GET or creates bans. Reports can be fabricated. Independently review evidence before approving; the report is a review request, not an evidence bundle.

### Approving or rejecting reports

In the private **Review** tab, change column E (`review_status`) to `approved`. The installed edit trigger adds one public `ban` entry for the validated friend code, with the fixed reason `Approved by AU-Anticheat list maintainer` and no expiry. The private claim, timestamp and plugin version are not copied. Column F (`sync_result`) shows success or a specific error, and column G (`synced_utc`) records the last successful synchronization.

`rejected`, `pending`, blank and other statuses add nothing. Rejecting a previously approved report does **not** remove an existing ban: remove it explicitly from the public list if needed, and separately remove any host's saved local persistent ban. This preserves independent bans and other approved reports about the same code.

Re-approving does not duplicate an active ban or change its existing reason/expiry. An existing watch entry or expired ban is promoted to a ban with no expiry. Duplicate/conflicting public rows, invalid codes/formulas, malformed headers and known test records are rejected with feedback. Approve at most 100 rows in a single paste. If synchronization fails, fix the stated cause and change the decision to pending, then approved to retry. Installing the trigger does not act on old approved rows automatically.

Only owner-authorized edits in the configured private Review tab invoke this workflow. API submissions cannot set approved status or fire the edit trigger. Google schedules trigger execution, so it is not instantaneous; clients still download the public list at most once every 15 minutes. Approval publishes a community moderation decision, not authenticated proof of cheating or an official account sanction.

## Settings and local files

Preferences automatically save to `BepInEx/config/AU-Anticheat/settings.json`. Confirmed reset restores defaults and clears entry exceptions, preserving written rules, loadouts and moderation lists; never undoes bans. Appearance defaults red/75% opacity. No accent selector. Key defaults Insert; Home/F1–F12 alternatives.

The directory also stores `lobby-rules.txt`, `lobby-loadouts.json`, `wardrobe.json`, `moderation-list.json` and four filter files. Exceptions use plugin configuration. Diagnostic data may remain in `BepInEx/LogOutput.log`. Current-lobby evidence is not automatically uploaded. Public messages remain visible to recipients. See [POLICY.md](POLICY.md).

## Building and testing

The Source ZIP builds the plugin. Tests ZIP includes matching source and test suites, not installation files. Use a .NET SDK targeting .NET 6 and your own generated game/BepInEx interop:

```powershell
$gamePath = 'C:/Program Files (x86)/Steam/steamapps/common/Among Us'
dotnet build source/AU-Anticheat.csproj -c Release "-p:GameDirectory=$gamePath"
dotnet run --project tests -c Release -- 'source/bin/Release/net6.0/AU-Anticheat.dll' "$gamePath/BepInEx/interop/Assembly-CSharp.dll"
dotnet run --project ui-tests -c Release
dotnet run --project lobby-tests -c Release
dotnet run --project role-tests -c Release
dotnet run --project movement-tests -c Release
node service-tests/review-reports.test.cjs
node service-tests/approval-sync.test.cjs
```

DLL: `source/bin/Release/net6.0/AU-Anticheat.dll`. No proprietary game binaries/interop distributed. Tests are not compiled into the plugin. Ordinary test runs use mocked Google APIs and make no external requests. An optional `lobby-tests -- --live-list` run checks the real CSV; `--live-report-probe` additionally writes one synthetic review record and must only be run with the service owner's consent. The live check is separate from assertion totals.

Final results/hashes are beside downloads in `VERIFICATION.txt` and `SHA256SUMS.txt`. Production Release build must have zero warnings/errors. Unused-field warnings in test substitutes are not native-runtime failures. Metadata inspection does not execute the game.

### Live checklist — not yet completed

Test privately with consenting players: startup/all panels; host/guest; opening versus post-meeting cooldown; racing body reports; murders/doors/sabotage; Engineer vents/cleaning; shapeshift/revert; Influencer/ghosts; ladders/platforms/lag; wardrobe edges; saved outfit inventory/native cosmetic requests; full-lobby levels/exceptions; rules every refill; manual/automatic chat collision; one summary including departed impostors; list save/remove/filter cancellation; host migration; join lock; auto-start/return; restart/reset; second-client extended values; x86 startup; report preview/submission inside native Unity. The separate HTTP-service receipt and inbox privacy checks passed; they do not prove the in-game UI works natively.

Do not certify these from managed tests alone or run disruptive tests publicly. Preserve exact popups/redacted logs.

## References

Fork credits in [README.md](README.md#credits-and-license). Targeted sources: [BanMod](https://github.com/GiannBart/BanMod/tree/0dc37d20325289fb8efe30e866da522e78e5525b), [HostGuard owner handling](https://github.com/RaresHonour/HostGuard/blob/2d7b5e77d33ca67ecb0b760623ac9eee53ea7337/HostGuard/Patches/RpcValidationPatch.cs), [Impostor ownership checks](https://github.com/Impostor/Impostor/blob/703915f06fcfd17bc8804d36dd67655671a66cf2/src/Impostor.Server/Net/Inner/InnerNetObject.Anticheat.cs) and [Innersloth host-modding documentation](https://github.com/Innersloth-LLC/AmongUsModdingInformation). These have different trust boundaries; object ownership is not connection authentication.

Prior scoped reviews included Town of Host/Enhanced and BetterAmongUs. This revision also reviews AmongUsRevamped classic task/device/sabotage controls; its DevicePatches informed temporary targeted comms disruption (GPL-3.0). No full audit of every menu/device/repository is claimed. NLayer is not bundled. No private services, external credentials or proprietary game source were copied.
