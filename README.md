# ExtraSpecialSlots v1.0.0 — SPT 4.1.6

Adds Special Slots 4–6 while keeping vanilla/SVM behavior and compatibility with TSC, SpecialSlots, and Fika.

## Features

- Adds missing `SpecialSlot4`, `SpecialSlot5`, and `SpecialSlot6`.
- Keeps Special Slots 1–3 untouched.
- 3 × 2 Special Slots UI layout:

```text
1 2 3
4 5 6
```

- Uses the verified Comfort Kit-style `SearchableSlotView.CreateSlots` / `_specSlotsPanel` layout approach.
- Does not increase inventory width, preventing the extra horizontal inventory scrolling caused by a six-wide row.
- Deep-clones vanilla/SVM slot filtering instead of sharing mutable filter lists.
- SpecialSlots (jbs4bmx) remains responsible for its own filtering when installed.
- Configurable TSC ownership of Special Slot 4.
- Fika Headless safety guard: uses a soft dependency on the official Fika Headless GUID so it loads after Headless when present, then disables the client/UI plugin before installing any patches. The SPT server mod continues to run normally.

## Install layout

```text
BepInEx\plugins\ExtraSpecialSlots\
    BlackHawk-ExtraSpecialSlots.Client.dll

SPT_Runtime\user\mods\ExtraSpecialSlots\
    BlackHawk-ExtraSpecialSlots.Server.dll
    config.jsonc
```

### Fika Headless

The recommended headless installation is still **server-side only**. Do not intentionally install the client DLL on the headless instance.

If the client DLL is present there by mistake, its soft dependency on the official Fika Headless plugin GUID `com.fika.headless` guarantees Headless loads first. ExtraSpecialSlots then detects it in BepInEx `Chainloader.PluginInfos`, disables itself before applying any UI/Harmony patches, and leaves the server-side mod running.

## Slot 4 config

`SPT_Runtime\user\mods\ExtraSpecialSlots\config.jsonc`:

```jsonc
{
  "SpecialSlot4Owner": "ExtraSpecialSlots"
}
```

Allowed values: `ExtraSpecialSlots` or `TSC`.

- `ExtraSpecialSlots`: Slot 4 follows the same vanilla/SVM filtering behavior as the other ExtraSpecialSlots slots.
- `TSC`: when TSC is installed, ExtraSpecialSlots leaves Slot 4 to TSC.
- If TSC is not installed, ExtraSpecialSlots controls Slot 4 automatically.

## SpecialSlots (jbs4bmx)

When SpecialSlots is installed, ExtraSpecialSlots makes sure the extra slots exist but does not try to override SpecialSlots filtering. SpecialSlots can apply its own configured filtering to the special slots.

## Build

From the project root in PowerShell:

```powershell
.\build.ps1
```

The script builds both projects, creates `dist`, and produces:

```text
release\ExtraSpecialSlots-v1.0.0.zip
```

That ZIP is the ready-to-install release package.
