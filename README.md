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

## Experimental bridge 3D preview

With a matching verified bridge cache installed, enable **Bridge 3D preview** in the minimap menu (`L`). Left drag orbits, right drag pans, the wheel zooms, and double click resets. Use **Close** to dismiss it. The preview closes on mission exit and does not load new scene geometry. It shows the cached bridge, not the whole town; proxy/original and height ceiling options also apply. Its interactive rendering is separate from the small map's cached GPU texture and CPU fallback. Native preview validation is pending.
