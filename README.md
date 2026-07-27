# Hotline - One Overlay and One Key for Every Mod's HUD

> 🛟 **Need help or found a bug?** Get support at [support.doodesch.de/hotline](https://support.doodesch.de/hotline).

> Every mod invents its own hotkey and its own debug window. Hotline ends that: one master key (F6) opens a
> single in-game overlay where every mod gets a clean, draggable panel - buttons, toggles, readouts, logs.
> It even auto-catches mods that grab function keys (F1-F12) and turns them into clickable buttons, so two
> mods can stop fighting over the same key. A framework other mods plug into - and a compatibility layer that
> works even for mods that never heard of it.

![Version](https://img.shields.io/badge/version-1.1.0-blue)
![Game](https://img.shields.io/badge/game-Schedule%20I-purple)
![MelonLoader](https://img.shields.io/badge/MelonLoader-0.7.3+-green)
![Type](https://img.shields.io/badge/type-framework-orange)
![Status](https://img.shields.io/badge/status-working-brightgreen)

## Features

- **One overlay, one key.** Press **F6** to open a single in-game overlay. Every mod that registers gets its
  own toggleable, movable, resizable window - no more remembering a different hotkey per mod.
- **Ready-made panels.** A registering mod gets text readouts, images (e.g. a QR code), action buttons, on/off
  toggles and a log channel for free, drawn by Hotline - it never has to build its own UI.
- **Auto-catch raw hotkeys.** Hotline detects mods that poll function keys (F1-F12) and adds each as a
  clickable button in that mod's panel, attributing it to the right mod automatically. Trigger any mod's
  hotkey from one place.
- **Optional full takeover.** Turn it on and a physical function-key press no longer reaches the mod at all -
  it opens Hotline focused on that mod's panel instead, so two mods can never collide on the same key.
- **Built for IL2CPP.** Cached-rebuild IMGUI windowing with polled input - the path that actually works for
  in-game mouse interaction on this build. No game canvas required, near-zero cost while hidden.
- **A real framework.** A zero-overhead, no-op-when-absent modder API (`Hotline.Api`): your mod lights up
  inside Hotline when it is installed and costs nothing when it is not, so there is no hard dependency.
- **Console control.** A full `hotline ...` console (overlay, panels, keys, takeover) for headless or
  keyboard-free control.

## Requirements

| Component | Version / Source |
|-----------|------------------|
| Schedule I | IL2CPP (current Steam public build) |
| MelonLoader | `0.7.3+` |

Hotline has no other hard dependency - it does not use S1API. Any mod can plug into it, but mods work fine
without it too.

## Installation

### Recommended: a Thunderstore mod manager

Install with a mod manager (r2modman / Gale) from the Schedule I community; MelonLoader is pulled in
automatically.

### Manual

1. Install **MelonLoader 0.7.3** for Schedule I.
2. Drop **`Hotline.dll`** into your Schedule I `Mods/` folder.

## Usage

Enter a save and press **F6** to open or close the overlay. The Overview window lists every mod's panel as a
button - click one to open that mod's window. Drag a window by its title bar, resize it from the bottom-right
corner. Anything you can do with the mouse you can also do from the console: `hotline help` lists it all.

If a mod uses its own raw function key, Hotline discovers it and adds a button to that mod's panel, so you can
trigger it without touching the key. Turn on full takeover (below) to make the physical key open Hotline
instead of the mod, ending key collisions entirely.

## Configuration

Settings live in `UserData/MelonPreferences.cfg` under
`Hotline_01_Main`. Changes apply live.

| Setting | Default | What it does |
|---|---|---|
| `Enabled` | `true` | Master on/off. Off = Hotline does nothing. |
| `MasterHotkey` | `F6` | The single key that opens/closes the overlay. Any UnityEngine.KeyCode name. Reserved for Hotline (never auto-intercepted). |
| `InterceptFunctionKeys` | `true` | Detect other mods' function keys (F1-F12) and add each as a clickable button in the overlay. |
| `SuppressRawFunctionKeys` | `false` | Full takeover: a physical function-key press opens the overlay on that mod's panel instead of reaching the mod, so two mods can't collide on a key. Off by default (the raw key keeps working and the button is just an addition). |
| `HudFontSize` | `12` | Overlay text size (px). The windows auto-resize to fit. |
| `ShowHud`, `HudX`, `HudY`, `WindowLayouts` | managed | Overlay visibility, overview position and saved window layouts (managed by dragging the windows or the console). |

## How it works

Hotline draws one overlay and owns one key, so the in-game UI of every mod lives in the same place. Mods that
adopt the API register a panel and Hotline draws it. Mods that do not are still covered: Hotline watches for
function-key polls during each mod's update, attributes the key to that mod, and surfaces it as a button. The
master key and any synthetic press it injects stay client-local, so nothing affects multiplayer.

## For modders

Reference `Hotline.Api.dll` or drop the single `Hotline.cs` file into your mod, then:

```csharp
using Hotline.Api;

Hud.RegisterPanel("MyMod", "My Mod")
   .Text(() => "queue = " + _queue.Count)
   .Action("Reload", Reload)
   .Toggle("Verbose", () => _verbose, v => _verbose = v)
   .Hotkey("Reload", HotlineKey.F8, Reload);   // optional central hotkey
```

Every call is a zero-overhead no-op when Hotline is not installed and lights up automatically when it is, so
you can ship it with no hard dependency.

## Compatibility

- IL2CPP build only (current Steam public branch).
- Works alongside any mod. Mods that poll legacy `UnityEngine.Input` function keys are auto-caught; mods that
  use the new Input System are not intercepted (they are simply not affected).
- Vanilla input is never touched - the game reads gameplay through its own input system, and interception is
  scoped to mod code.

## Credits

- **DooDesch** - mod author.

## License

Provided as-is under the [MIT License](LICENSE.md).
