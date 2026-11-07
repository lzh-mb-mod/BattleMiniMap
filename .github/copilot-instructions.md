# Copilot instructions for BattleMiniMap

## Build, test, and lint

### Prerequisites

- Initialize the shared library before building: `git submodule update --init --recursive`.
- Builds require Mount & Blade II: Bannerlord assemblies through the MSBuild `GamePath` property. The local default is in `source\library\mission-library\source\BasicSharedLibrary\GamePath.props`; override it with `/p:GamePath="<Bannerlord path>\"` when needed.
- Use a Windows MSBuild environment because the project post-build targets invoke `rmdir` and `xcopy`.

### Build commands

- Restore and build the same configuration used by CI:
  ```powershell
  nuget restore .\source\BattleMiniMap.sln
  MSBuild.exe .\source\BattleMiniMap.sln /p:Configuration=Release /p:GamePath="<Bannerlord path>\"
  ```
- Build only the main module and its project references:
  ```powershell
  MSBuild.exe .\source\BattleMiniMap\BattleMiniMap.csproj /p:Configuration=Debug /p:GamePath="<Bannerlord path>\"
  ```
- Build only the custom widget assembly:
  ```powershell
  MSBuild.exe .\source\BattleMiniMap.Widgets\BattleMiniMap.Widgets.csproj /p:Configuration=Debug /p:GamePath="<Bannerlord path>\"
  ```
- A successful main-project build recreates `source\package\BattleMiniMap`, copies the module assets and required DLLs there, and deploys the result to `<GamePath>\Modules\BattleMiniMap`. Do not treat these post-build writes as unrelated side effects.

### Tests and lint

- No automated test project or lint command is defined, so there is no full-suite or single-test command. Validate gameplay and Gauntlet UI changes in a Bannerlord battle after building.

### Packaging and release

- `.github\workflows\Build.yml` is the authoritative CI path. It initializes submodules, downloads the configured Bannerlord depots, restores with NuGet, builds the Release solution with `GamePath` pointing at those binaries, and uploads `source\package`.
- CI generates packaged HTML documentation from the repository-root Markdown files after the build:
  ```powershell
  Set-Location .\source\BattleMiniMap
  dotnet md2html -i ..\..\README.md -o ..\package\BattleMiniMap\README.html
  dotnet md2html -i ..\..\README.zh-CN.md -o ..\package\BattleMiniMap\README.zh-CN.html
  dotnet md2html -i ..\..\CHANGELOG.md -o ..\package\BattleMiniMap\CHANGELOG.html
  ```
- Release tags use the `release-v*` trigger; current version-specific tags include the target game version, for example `release-v3.2.0-for-bannerlord-v1.4.6`. The workflow also retains legacy `release-b*` and `release-e*` triggers.
- `master` and version branches such as `v1.3.15` represent different Bannerlord compatibility lines. Confirm the target branch before changing TaleWorlds references, dependency versions, or release notes.
- For a release, keep these sources synchronized:
  - `.github\resources\config.env`: `GAME_VERSION`, Steam depot `GAME_BRANCH`, and `MOD_VERSION`;
  - `source\BattleMiniMap\Modules\BattleMiniMap\SubModule.xml`: module version and all `DependentVersion` values;
  - `CHANGELOG.md`: the user-visible version entry;
  - Steam Workshop XML compatibility tags and change notes when publishing there.
- GitHub release archives are produced only for matching release tags. The artifact name is derived from `config.env`, while the zip contains the complete `source\package` tree.
- Steam Workshop publishing is separate from GitHub Actions. Run `source\BattleMiniMap\PublishToSteamWorkshop.ps1` to create an item or `source\BattleMiniMap\UpdateToSteamWorkshop.ps1` to update item `3754292714`; both read the executable location from MissionLibrary's `GamePath.props` and upload the already-built `source\package\BattleMiniMap`.
- `UpdateToSteamWorkshop.ps1` intentionally uses `SteamWorkshopUpdate-without-description.xml`, preserving the existing Workshop description while updating content, tags, and optional change notes. The XML files contain machine-local absolute content/image paths, so review them before publishing from another checkout.

## High-level architecture

- `source\BattleMiniMap` is the `net472` Bannerlord module. `BattleMiniMapSubModule` delegates staged initialization to MissionLibrary, then registers the mission-start handler, hotkeys, usage text, and options menu.
- `MissionStartingHandler` adds one `BattleMiniMapView` to each mission. That view owns the Gauntlet layer, initializes terrain data, listens for boundary and order-view changes, processes map hotkeys, updates camera state every frame, and refreshes agent state on a 0.05-second timer.
- `BattleMiniMapViewModel` is the bridge between mission state and `Modules\BattleMiniMap\GUI\Prefabs\BattleMiniMapView.xml`. It exposes fade/opacity state, camera markers, and separate hero/non-hero marker collections.
- `MiniMap.Instance` is the shared terrain-map service and currently creates `GdiMiniMap`. It derives map bounds from mission boundaries, samples terrain/water/navmesh data into a `System.Drawing.Bitmap`, converts that bitmap to a TaleWorlds texture, and releases the previous engine texture when replacing it. `SimpleMiniMap` is an alternate implementation but is not selected by the singleton.
- Rendering is split across the main module and `source\BattleMiniMap.Widgets`, a `netstandard2.0` assembly containing lower-level Gauntlet draw primitives. Main-project widgets compose the map, boundary, camera, and agent layers. Agent markers are repainted into a map-space render target when marker collections change, then displayed as one texture transformed with the terrain.
- Follow mode and full-map mode share the conversion helpers in `src\Utility.cs`. Follow mode rotates/scales around the combat camera and uses circular clipping; full-map mode maps the complete mission bounds into the configured rectangular widget.
- Configuration, hotkey persistence, menu infrastructure, mission startup abstractions, and shared assets come from the `source\library\mission-library` submodule. Avoid duplicating that infrastructure in BattleMiniMap.

### Decompiled Bannerlord reference source

- Decompiled Bannerlord source is available locally under versioned directories in `D:\develop\src\Bannerlord.Binaries`. Treat it as read-only reference code, not as part of this repository.
- Search the matching game-version tree before guessing TaleWorlds/Gauntlet APIs, lifecycle behavior, coordinate handling, or method control flow. Match the reference version to the target branch and `.github\resources\config.env`.
- Use `.github\scripts\Decompile-Bannerlord.ps1` to regenerate the reference source. It detects the game version from `Modules\Native\SubModule.xml` and writes to a versioned output directory.
- Use `-BannerlordRoot` for a non-default game installation, `-DecompilerOutputRoot` to change the reference root, `-InstallTool` to install `ilspycmd`, and `-Assemblies` to regenerate only relevant assemblies. Omitting `-Assemblies` processes the script's complete assembly map.
- Example:
  ```powershell
  .\.github\scripts\Decompile-Bannerlord.ps1 `
    -DecompilerOutputRoot D:\develop\src\Bannerlord.Binaries `
    -Assemblies TaleWorlds.MountAndBlade,TaleWorlds.GauntletUI,TaleWorlds.TwoDimension
  ```

## Key conventions

- Treat files under `source\BattleMiniMap\Modules\BattleMiniMap` as the canonical module assets. The project copies them into `source\package` during the post-build step; do not edit generated package copies instead.
- A new persisted option normally requires coordinated changes to:
  - `BattleMiniMapConfig`: property/default, `CopyFrom`, and—when compatibility requires it—`BinaryVersion` plus `UpgradeToCurrentVersion`;
  - `BattleMiniMapOptionClassFactory`: MissionLibrary option view model and callback;
  - `module_strings.xml`, `Languages\std_BattleMiniMap.xml`, and `Languages\CNs\BattleMiniMap-zho-CN.xml`: source string and English/Chinese localization entries.
- Preserve the two-part localization ID pattern: code looks up `str_battle_mini_map_*`; `module_strings.xml` maps that to a `{=BattleMiniMap_*}` ID; language files translate the `BattleMiniMap_*` ID.
- Gauntlet custom widget class names intentionally match XML element names such as `BattleMiniMap_MapWidget` and `BattleMiniMap_AgentMarkerCollectionWidget`. View-model values bound in XML must remain public and notify through `OnPropertyChanged` when their runtime value changes.
- Keep world/map/widget axis swaps and follow-mode transforms centralized in the `IMiniMap` extension methods in `src\Utility.cs`. Marker, boundary, camera, and background rendering must use the same conversions or their layers will drift or rotate differently.
- Agent markers are updated in place. Inactive entries are removed by copying the last active marker into the gap and shrinking `CountOfAgentMarkers`; preserve this low-allocation approach in mission-tick code.
- Agent markers are rendered individually by `BattleMiniMap_AgentMarkerCollectionWidget` using shared textures and materials. Preserve marker shape, tint, layer order, and the distinct full-map/follow-mode transforms when changing this path.
- Terrain image regeneration is intentionally selective: resolution changes rebuild map size and image, unwalkable-terrain changes resample the image, and edge-opacity changes only update alpha. Keep `BattleMiniMapConfig.OnMenuClosed` aligned with any new terrain-affecting setting.
- Release version metadata is duplicated in `.github\resources\config.env` and `source\BattleMiniMap\Modules\BattleMiniMap\SubModule.xml`. Keep `MOD_VERSION`/`GAME_VERSION` synchronized with the module `Version` and dependent module versions when preparing a release.
