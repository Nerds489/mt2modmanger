# Monster Train 2 Mod Manager (BepInEx)

In‑game mod manager UI for Monster Train 2 built on BepInEx + Harmony.  
Toggle mods, open configs, export lists, and manage everything without leaving the game.

## Build

```bash
dotnet build -c Release -p:BepInExDir="C:\Games\MonsterTrain2\BepInEx"
```

## Install (after build)

Copy `bin/Release/net48/MT2ModManager.dll` to:
```
<MonsterTrain2>/BepInEx/plugins/MT2ModManager/
```

Hit **F1** to toggle the menu (configurable).


VERY LIGHTWEIGHT DOES NOTHING RIGHT NOW BUT SHOW YOU YOUR MODS INSTALLED

