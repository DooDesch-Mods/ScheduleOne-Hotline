# Changelog

All notable changes to Hotline are documented here. This project adheres to
[Semantic Versioning](https://semver.org/).

## [1.2.1] - 2026-08-04

### Fixed

- The overlay no longer takes the game down when a mod's panel shows an image. Opening it with Snitch
  installed crashed straight to desktop, because the way the image was drawn does not exist in this
  build of the game.
- A panel that fails to draw is skipped instead of killing the whole overlay, and it says so once in
  the log rather than sixty times a second.

## [1.2.0] - 2026-07-31

### Added
- Panels can now hold a **slider** (`Panel.Slider`, `Hud.RegisterSlider`) - a value you drag rather than a
  number you type. The host clamps it to the declared range and snaps it to the step before your setter runs,
  so a setter never sees a value it did not declare as legal, whichever control wrote it.
- `hotline slider <sliderId> [value]` reads or writes the same value from the console. A slider is a mouse
  control and a mouse control cannot be driven by a test harness, so anything reachable only by dragging
  reaches testers unverified.

### Changed
- Dragging a slider continues while the cursor is off the track, which is how every slider anywhere behaves.

### Notes
- Additive and backward-compatible: older mods are unaffected, and a mod that registers a slider is a no-op
  on an older Hotline.
- Control ids are slugified from the label and punctuation is dropped, so two labels that differ only in
  punctuation collide and the second replaces the first. Give each control distinct words.

## [1.1.0] - 2026-07-08

### Added
- Panels can now show an **image** (`Panel.Image`) alongside their text, actions and toggles - a mod supplies
  raw pixels and the overlay draws them crisply. Additive and backward-compatible: older mods are unaffected,
  and a mod that uses it is a no-op on an older Hotline. First user: Snitch's in-game connect-a-phone QR.

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
