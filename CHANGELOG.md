# Changelog

## 1.1.0 — SPT 4.1.6

- Fixed the map transit transfer screen freezing when it opens.
- Fixed the backpack sometimes missing from the transfer screen.
- Built the server mod with SPT 4.1.6.

**Fika Headless:** Do not install the client DLL in `BepInEx\plugins` on the headless PC. Install the server mod in `SPT_Runtime\user\mods` on the SPT server.

## 1.0.0

- Initial public release.
- Adds Special Slots 4–6.
- 3 × 2 special-slot layout without expanding inventory width.
- Vanilla/SVM filter inheritance.
- SpecialSlots filtering compatibility.
- Configurable TSC ownership for Special Slot 4.
- Fika Headless client-plugin safety guard using a soft dependency plus the official `com.fika.headless` BepInEx GUID.
