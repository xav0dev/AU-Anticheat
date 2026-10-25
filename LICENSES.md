# Licenses and bundled components

## AU-Anticheat

Hydra-derived fork: **GPL-3.0**, see [LICENSE](LICENSE). Upstream: [MrDiamond64/Hydra](https://github.com/MrDiamond64/Hydra). Additional AU-Anticheat work by xav0dev - Boo. Required credits remain in [README.md](README.md#credits-and-license).

Distribute corresponding AU-Anticheat source alongside the DLL/installation releases. The Source ZIP contains this revision's source and build instructions; Tests ZIP is separate. Renaming does not remove upstream attribution.

## Installation loader

Installers redistribute unmodified files from official **BepInEx Unity IL2CPP Windows build 788**, commit `5b766a3`, plus the plugin/docs/licenses. The loader is not authored by this fork.

- [Official build downloads](https://builds.bepinex.dev/projects/bepinex_be/788).
- [BepInEx source at the packaged revision](https://github.com/BepInEx/BepInEx/tree/5b766a3), LGPL-2.1 license text in `licenses/BepInEx-LICENSE.txt`.
- [UnityDoorstop 4.5.0 source](https://github.com/NeighTools/UnityDoorstop/tree/v4.5.0), LGPL-2.1 notice in `licenses/UnityDoorstop-LICENSE.txt`.
- [Il2CppInterop 1.5.3 source](https://github.com/BepInEx/Il2CppInterop/tree/v1.5.3), LGPL-3.0 notice in `licenses/Il2CppInterop-LICENSE.txt`.
- [.NET runtime 6.0.7 source](https://github.com/dotnet/runtime/tree/0ec02c8c96e2eda06dc5b5edfdbdba0f36415082), MIT license and third-party notices in `licenses/dotnet-*.txt`.

Other loader components include HarmonyX, MonoMod, Mono.Cecil, AsmResolver, Cpp2IL/LibCpp2IL, AssetRipper components, Disarm, Iced, Capstone, Dobby, SemanticVersioning, StableNameDotNet and WasmDisassembler. Their own copyright/license terms apply; additional retained notices and source references are in `licenses/`. AU-Anticheat's GPL does not relicense those components. The official loader changelog is retained.

These are pinned loader packages, not a promise that an older embedded runtime receives ongoing security fixes. Do not substitute arbitrary unverified loader binaries. Preserve notices and applicable source availability when republishing.

## Game and research

Among Us and its original assets belong to Innersloth. No game executable, generated interop, paid cosmetics or proprietary source is bundled. The plugin uses fonts exposed by the installed game, not a redistributed game-font asset.

`source/anticheat/BuiltInDefaultNames.cs` adapts HostGuard's `DefaultNameChecker.cs` at revision `2d7b5e77d33ca67ecb0b760623ac9eee53ea7337`, copyright (c) 2026 Rares, under MIT. The complete notice is retained in `licenses/HostGuard-MIT.txt`. HostGuard's configured TNT, auser and Haunt Bot name examples are also included. No upstream player-allegation database is imported.

BanMod and other named projects are targeted design references, not bundled private services or endorsements. No NLayer component is bundled. The MIT component does not relicense this Hydra-derived fork.

Selected classic gameplay designs were reviewed against [AmongUsRevamped](https://github.com/ApeMV/AmongUsRevamped), GPL-3.0. In particular its DevicePatches informed the targeted-comms method in `LobbyDeviceRules.cs`; task/sabotage/settings controls are independently implemented. The supplied compiled AUR archive was inspected as reference only and is not redistributed. Its full set of gamemodes is not included.
