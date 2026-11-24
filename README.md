# Battle Mini Map

A mod for Mount&Blade II Bannerlord that provides mini map in battle.

## Game Compatibility
Use this build with Bannerlord v1.5.6 Beta. For Bannerlord v1.4.8, use the stable build from [GitHub Releases](https://github.com/lzh-mb-mod/BattleMiniMap/releases).

## Save Compability
This mod does not read or write stuffs to your game save. Installing or removing this mod has no impact on your game save.

## Features
- Show units position in the minimap.

- Display terrain height in different colors.

## How to Use
- The map is shown by default after installation.

- You can press `L` during a battle to open the mod menu and change the size, position, resolution and opacity of the map.
## Troubleshoot
- If the launcher can not start:

  - Uninstall all the third-party mods and reinstall them one by one to detect which one cause the launcher cannot start.

- If it shows "Unable to initialize Steam API":

  - Please start steam first, and make sure that Bannerlord is in your steam account.

- If the game crashed after starting:
  
  - I would appreciate it if you send dump file to me to help me to solve the crash by followinig steps below:

    - Click `Yes` when the game crashes and ask whether to collect information.

    - Before sending files to TaleWorlds, go to `C:\ProgramData\Mount and Blade II Bannerlord\crashes`(**Not the game installation path**) and find the folder related to the crash by timestamp.

    - Then send `dump.dmp` file in the folder to me.

## Source Code

You can get it at [github.com](https://github.com/lzh-mb-mod/BattleMiniMap).

## Contact with me
- Email:

  - lizhenhuan1019@outlook.com

  - lizhenhuan1019@qq.com

## Experimental runtime 3D map

Enable **3D map preview** in the minimap menu (`L`). It incrementally extracts collision shapes and original physical terrain heightfields from the loaded scene, recording time and coverage. Disable **Use collision geometry** to compare runtime render meshes. Left drag orbits, right drag pans, wheel zooms, double click resets. Closing the preview or leaving the mission cancels extraction.

Runtime mode does not read prebuilt scene/model caches. The collision reader currently requires the verified local engine build. Spheres/capsules are display approximations; terrain is rebuilt at its original physical heightfield resolution, preserving holes and diagonals. Turn off **Read original runtime terrain** for the old height-query comparison. Raw reads require the verified engine/PhysX build; rejected data is reported as incomplete, without automatic disk or sampled fallback. Five height queries per tile validate coordinates only; they do not generate geometry. Unknown shapes, anonymous merged physics, skeletal/cloth meshes and in-place geometry edits have coverage limits. This iteration still needs native game validation. Bridge proxy and clipping options apply only to the old cache diagnostic, selected by disabling runtime extraction. The main map retains its CPU fallback.

Raw runtime terrain defaults to map simplification: complete patch borders and holes are retained; patches exceeding 0.10 m vertical error keep their original topology. Disable **Simplify runtime terrain** to compare full topology. Upload batches cover 64 by 64 cells. Logs separate source/display faces, accepted error, terrain completion time and collision rejection reasons. Actual reduction and performance still require scene validation.

Raw terrain file verification runs in a cancellable background task on each open, followed by loaded-module identity checks before memory access. Closing the preview retires pending initialization. Logs distinguish background verification, waiting and main-thread work durations.

The minimap layer passes right-button gestures through, including release after RTS camera cursor recentering. Right drags begun inside the visible 3D preview remain owned by preview panning. Map display size is unchanged.
