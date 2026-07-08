# Hotline - One Hotkey for Every Mod's HUD

> 🛟 **Need help or found a bug?** Get support at [support.doodesch.de](https://support.doodesch.de).

> **One overlay and one key for every mod's HUD.** Press F6 to open a single in-game overlay where every mod
> gets a clean, draggable panel - buttons, toggles, readouts, logs. Hotline even auto-catches mods that grab
> function keys (F1-F12) and turns them into clickable buttons, so two mods stop fighting over the same key.

![Version](https://img.shields.io/badge/version-1.1.0-blue)
![Game](https://img.shields.io/badge/game-Schedule%20I-purple)
![MelonLoader](https://img.shields.io/badge/MelonLoader-0.7.3+-green)
![Type](https://img.shields.io/badge/type-framework-orange)

## Features

- **One overlay, one key.** Press **F6** for a single in-game overlay; every mod that registers gets its own
  toggleable, movable, resizable window. No more a different hotkey per mod.
- **Ready-made panels.** A registering mod gets text readouts, images (e.g. a QR code), action buttons, on/off toggles and a log
  channel for free - Hotline draws them.
- **Auto-catch raw hotkeys.** Hotline detects mods that poll function keys (F1-F12) and adds each as a button
  in that mod's panel, attributed to the right mod automatically.
- **Optional full takeover.** Turn it on and a physical function-key press opens Hotline on that mod's panel
  instead of reaching the mod, so two mods can never collide on a key.
- **A real framework.** A zero-overhead no-op-when-absent modder API, so mods can adopt it with no hard
  dependency.

## Requirements

- **Schedule I** (IL2CPP) with **MelonLoader 0.7.3+**.
- Optional: **Mod Manager & Phone App** for the in-game settings UI.

Hotline has no other hard dependency - it does not use S1API.

## Usage

Enter a save and press **F6** to open or close the overlay. The Overview lists every mod's panel - click one
to open its window; drag by the title bar, resize from the corner. `hotline help` lists the full console.

## Settings

`Enabled`, `MasterHotkey` (F6), `InterceptFunctionKeys` (on), `SuppressRawFunctionKeys` (off, full takeover),
`HudFontSize` (12). Editable in the Mod Manager & Phone App UI or `UserData/MelonPreferences.cfg` under
`Hotline_01_Main`.

## License

MIT. See the included LICENSE.md.
