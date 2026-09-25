# v1.1.1

- Fixed item icons disappearing after reopening Character.
- Fixed missing first slot and backpack overlap.
- Keeps the transit screen fix.

For SPT 4.1.6. Extract the ZIP into your SPT folder. Existing users only need to replace the client DLL; keep your server config.

**Fika Headless:** Do not install the client side (`BepInEx`) on the headless server machine. Install it on playing PCs only. Keep the server component on the SPT backend.

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

