# Privacy, safety and publication notes

AU-Anticheat is an independent, experimental community mod, not an Innersloth service. It is not a guarantee against cheating, removal attacks, malware or network denial of service.

## Local data

Preferences, rules, exceptions, named loadouts, your wardrobe presets and moderation lists are saved on your device. Current-lobby evidence is bounded in memory; BepInEx diagnostics may persist to disk. Game-reported names, IDs and activity may appear in those logs. Redact them before sharing. The plugin does not scan other devices or deliberately collect hardware IDs.

## Optional network services

Online list lookup defaults OFF and downloads the configured public Google Sheet. Google sees normal connection metadata, including your IP. No player list is uploaded for lookup. The maintainer's public sheet URL is intentionally present in source/binaries.

Reports require a preview and an explicit submission. New installations contain the maintainer's public Google Apps Script endpoint; existing blank preferences stay disabled. The URL can be changed or cleared in-menu. Reports go to the maintainer's separate private inbox and contain the selected friend code, fixed unverified-suspicion text and version. The service/Google can observe connection IPs even though IPs are not included in the report body. Submissions never approve themselves or create bans. When the owner explicitly changes review_status to approved, an installed approval trigger can publish the friend code, ban status and a fixed moderation reason to the public list, without copying the private claim. Rejected/pending decisions publish nothing. Service operators must independently provide appropriate retention, correction and appeal procedures. A synthetic test receipt was verified; no real player was reported during preparation. Endpoint and public-list URLs are intentional public configuration, not secret credentials.

Reports, friend codes and referenced ownership are untrusted claims, not authenticated identities. Persistent-ban saving and owner punishment default ON by request; spoofed actions can cause incorrect local bans. Disable owner punishment for review-only attribution and review mistakes promptly.

## Testing and distribution

Use consenting private-lobby testing. Never test disruptive exploit payloads against public users. Do not publish personal logs, player records, account credentials or generated proprietary game libraries.

The built-in mod stamp remains hidden at the user's explicit request; only the AU-Anticheat X badge is retained. This differs from [Innersloth's published mod policy](https://www.innersloth.com/among-us-mod-policy/), which requires its mod stamp. **Do not describe this candidate as mod-policy compliant or Innersloth-approved.** Resolve that publication issue before public distribution. Nothing here changes game-server terms or grants permission to bypass sanctions.

Optional gameplay-changing settings use the [documented host-only mod registration](https://github.com/Innersloth-LLC/AmongUsModdingInformation) selected before hosting. This does not imply all custom values or future server versions support them.

The packages were not installed or published automatically. x86 and live multiplayer behavior remain unverified. Automated checks are regression evidence, not security certification.
