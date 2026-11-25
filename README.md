# RoseTrap

A lightweight Roblox launcher for Windows: FPS cap, potato mode, rendering options, fast flags and presets, wrapped in a rose-themed UI.

Built with C# / .NET 8 and [Avalonia 11](https://avaloniaui.net) (Skia-rendered, fully styleable, trimmable).

## Download

Grab `RoseTrap-vX.Y.Z-win-x64.exe` from [Releases](https://github.com/Kitte414414/RoseTrap/releases). It's one self-contained file (Windows 10/11, x64), so there's nothing to install and no .NET needed. Windows SmartScreen may warn because the exe isn't code-signed yet: choose *More info → Run anyway*.

New releases are built by GitHub Actions on Windows whenever a `v*` tag is pushed (see `.github/workflows/release.yml`).

## Layout

| Path | What's there |
| --- | --- |
| `src/RoseTrap.Core` | All non-UI logic, no dependencies: flag composition, `ClientAppSettings.json` writing, Roblox's `GlobalBasicSettings_13.xml`, install/update, launch, `roblox-player:` link handling. |
| `src/RoseTrap` | The Avalonia app: theme, pages, the character-drawn rose, the vine frame, tab transitions. |
| `tests/RoseTrap.Core.Tests` | Unit tests for the core. |
| `tests/RoseTrap.UiTests` | Headless UI tests that render every page with Skia and save screenshots (`ROSETRAP_SNAPSHOTS=<dir>`). |
| `design/` | The HTML prototype and the app icon source. |

## Build

```sh
dotnet test                                              # core + UI tests
dotnet run --project src/RoseTrap                        # run
dotnet publish src/RoseTrap -c Release -r win-x64        # trimmed single-file exe
```

## How settings reach Roblox

- **FPS cap and graphics quality** go into Roblox's own `%LocalAppData%\Roblox\GlobalBasicSettings_13.xml` (`FramerateCap`, `SavedQualityLevel`). Since September 2025 this is the only route that works: `DFIntTaskSchedulerTargetFps` is no longer honoured.
- **Fast flags** are merged into `ClientAppSettings.json`, next to the player and, optionally, in the global `%LocalAppData%\Roblox\ClientSettings`. RoseTrap only removes flags it owns, so entries you add by hand survive. The first write keeps a `.rosetrap.bak` backup of the original.
- **Allowlist only** (on by default) writes just the [18 flags Roblox still accepts](https://devforum.roblox.com/t/allowlist-for-local-client-configuration-via-fast-flags/3966569) and cleans the rest out of the file. Options that depend on legacy flags are marked "legacy" in the UI.
- **Install:** RoseTrap keeps its own copy of the player in `%LocalAppData%\RoseTrap\Versions`. It downloads packages from `setup.rbxcdn.com`, checks their MD5, and swaps in a new version only once it's complete. If it can't reach Roblox, it falls back to an existing official install.

## Credits

Core behaviour is adapted from [Masterstrap](https://github.com/Masterstrap/Masterstrap) (MIT).

Icons: [Fluent UI System Icons](https://github.com/microsoft/fluentui-system-icons) (MIT) and the "screwdriver-wrench" icon from [Font Awesome Free](https://fontawesome.com) (CC BY 4.0). `design/tools/icons.py` regenerates `src/RoseTrap/Theme/IconData.g.cs` from the npm packages.
