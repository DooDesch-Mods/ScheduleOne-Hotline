# Changelog

All notable changes to Hotline are documented here. This project adheres to
[Semantic Versioning](https://semver.org/).

## [1.0.0] - 2026-06-26

Initial release.

### Added
- One in-game overlay for every mod's HUD, opened by a single master key (default F6). Each registering mod
  gets its own toggleable, movable, resizable window.
- Modder API (`Hotline.Api`) - a zero-overhead no-op when Hotline is absent: register a panel with text
  readouts, action buttons, on/off toggles, a log channel and an optional central hotkey.
- Auto-interception of other mods' function keys (F1-F12): each polled key is attributed to its mod and
  surfaced as a clickable button in that mod's panel.
- Optional full takeover (`SuppressRawFunctionKeys`, default off): a physical function-key press opens the
  overlay on the owning mod's panel instead of reaching the mod, so two mods can no longer collide on a key.
- IMGUI windowing with polled input (the path that works for in-game mouse interaction on this IL2CPP build);
  draggable, resizable, scrollable windows with persisted layouts, near-zero cost while hidden.
- `hotline ...` console: control the overlay, panels, the master key, the central hotkey table and the
  takeover behaviour from the in-game console.
- A combined log timeline across all mods that report to Hotline.
