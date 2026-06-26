using System;
using MelonLoader;
using UnityEngine;

namespace Hotline.Config
{
    /// <summary>
    /// MelonPreferences wrapper. The category id is prefixed with the mod name ("Hotline_...") so it is
    /// auto-detected by the "Mod Manager &amp; Phone App" settings UI. Hotline ships its product surface (the unified
    /// overlay, the console and the central hotkey table) but stays quiet until you summon the overlay with the
    /// master key, so the defaults here are safe.
    /// </summary>
    internal static class Preferences
    {
        private const string CategoryId = "Hotline_01_Main";

        private static MelonPreferences_Category _category;

        private static MelonPreferences_Entry<bool> _enabled;
        private static MelonPreferences_Entry<bool> _showHud;
        private static MelonPreferences_Entry<string> _masterKey;
        private static MelonPreferences_Entry<int> _hudFontSize;
        private static MelonPreferences_Entry<float> _hudX;
        private static MelonPreferences_Entry<float> _hudY;
        private static MelonPreferences_Entry<string> _windowLayouts;
        private static MelonPreferences_Entry<bool> _interceptKeys;
        private static MelonPreferences_Entry<bool> _suppressRawKeys;

        internal static void Initialize()
        {
            if (_category != null) return;

            _category = MelonPreferences.CreateCategory(CategoryId, "Hotline (Mod HUD & Hotkey Hub)");

            _enabled = Create("Enabled", true, "Enable Hotline",
                "Master switch. When OFF, Hotline does nothing at all. When ON, the unified overlay and the central " +
                "hotkey table are available; the overlay stays hidden until you press the master key.");
            _showHud = Create("ShowHud", false, "Show overlay",
                "Whether the Hotline overlay is currently visible. Toggle live with the master key (default F7) or " +
                "'hotline hud'. Off by default.");
            _masterKey = Create("MasterHotkey", "F6", "Master overlay key",
                "The single key that opens/closes the Hotline overlay - the one hotkey that replaces every mod's own. " +
                "Any UnityEngine.KeyCode name (e.g. F6, F4, Backslash). Defaults to F6. This key is reserved for Hotline " +
                "and is never auto-intercepted.");
            _hudFontSize = Create("HudFontSize", 12, "Overlay font size",
                "On-screen overlay text size (px). The windows auto-resize to fit. Clamped 8-32. Change live with " +
                "'hotline hud font <n>' or by dragging a window's bottom-right corner.",
                new MelonLoader.Preferences.ValueRange<int>(8, 32));
            _hudX = Create("HudX", 8f, "Overview position X",
                "Overview window left edge in pixels. Kept on-screen automatically. Change live with " +
                "'hotline hud move <x> <y>' or by dragging.",
                new MelonLoader.Preferences.ValueRange<float>(0f, 4000f));
            _hudY = Create("HudY", 8f, "Overview position Y",
                "Overview window top edge in pixels. Kept on-screen automatically. Change live with " +
                "'hotline hud move <x> <y>' or by dragging.",
                new MelonLoader.Preferences.ValueRange<float>(0f, 4000f));
            _windowLayouts = Create("WindowLayouts", "", "Overlay window layouts (managed)",
                "Internal: saved positions, sizes and visibility of the overlay windows (the overview, every mod's " +
                "panel and the log timeline). Managed by dragging the windows or the 'hotline panel ...' console - " +
                "you normally do not edit this by hand.");
            _interceptKeys = Create("InterceptFunctionKeys", true, "Auto-intercept mod function keys",
                "ON (default): detect other mods that bind function keys (F1-F12), attribute each to the mod, and add " +
                "it to the overlay as a clickable button - so you can trigger any mod's hotkey from one place. Only the " +
                "polling mod ever receives the synthetic press; vanilla input is never touched. Turn OFF to disable.");
            _suppressRawKeys = Create("SuppressRawFunctionKeys", false, "Take over raw function keys",
                "OFF (default): mods' function keys keep working normally; Hotline only ADDS a clickable button for each " +
                "discovered key in the overlay. ON: a physical function-key press no longer reaches the mod - instead it " +
                "opens this overlay focused on that mod's panel, where each of its keys is a button you click. Turn ON to " +
                "stop two mods from fighting over the same key (the aggressive 'full takeover' mode).");
        }

        private static MelonPreferences_Entry<T> Create<T>(string id, T def, string name, string desc = null,
            MelonLoader.Preferences.ValueValidator validator = null)
        {
            return validator == null
                ? _category.CreateEntry(id, def, name, desc)
                : _category.CreateEntry(id, def, name, desc, false, false, validator);
        }

        // ----- accessors -----

        internal static bool Enabled => _enabled?.Value ?? true;

        internal static bool ShowHud => _showHud?.Value ?? false;
        internal static void SetShowHud(bool v) { if (_showHud != null) _showHud.Value = v; }

        /// <summary>The configured master key, parsed from its KeyCode name; falls back to F6 on a bad value.</summary>
        internal static KeyCode MasterKey
        {
            get
            {
                string s = _masterKey?.Value;
                if (!string.IsNullOrEmpty(s) && Enum.TryParse(s, true, out KeyCode k) && k != KeyCode.None) return k;
                return KeyCode.F6;
            }
        }
        internal static void SetMasterKey(string name)
        {
            if (_masterKey != null && !string.IsNullOrEmpty(name) && Enum.TryParse(name, true, out KeyCode k) && k != KeyCode.None)
                _masterKey.Value = k.ToString();
        }

        internal static int HudFontSize => Mathf.Clamp(_hudFontSize?.Value ?? 12, 8, 32);
        internal static void SetHudFontSize(int v) { if (_hudFontSize != null) _hudFontSize.Value = Mathf.Clamp(v, 8, 32); }

        internal static float HudX => _hudX?.Value ?? 8f;   // screen-clamped at draw time
        internal static float HudY => _hudY?.Value ?? 8f;   // screen-clamped at draw time
        internal static void SetHudPos(float x, float y) { if (_hudX != null) _hudX.Value = x; if (_hudY != null) _hudY.Value = y; }

        internal static string WindowLayouts => _windowLayouts?.Value ?? "";
        internal static void SetWindowLayouts(string v) { if (_windowLayouts != null) _windowLayouts.Value = v ?? ""; }

        internal static bool InterceptFunctionKeys => _interceptKeys?.Value ?? true;
        internal static void SetInterceptFunctionKeys(bool v) { if (_interceptKeys != null) _interceptKeys.Value = v; }

        internal static bool SuppressRawKeys => _suppressRawKeys?.Value ?? false;
        internal static void SetSuppressRawKeys(bool v) { if (_suppressRawKeys != null) _suppressRawKeys.Value = v; }
    }
}
