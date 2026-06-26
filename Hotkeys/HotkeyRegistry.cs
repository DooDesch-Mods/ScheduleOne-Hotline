using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hotline.Hotkeys
{
    /// <summary>One centrally-managed hotkey: a mod-owned action bound to a key. Hotline polls these every in-world
    /// frame so individual mods do not each run their own <c>Input.GetKeyDown</c> loop.</summary>
    internal sealed class HotkeyBinding
    {
        public string Owner;
        public string Label;
        public KeyCode Key;
        public Action Run;
    }

    /// <summary>
    /// The central hotkey table. Mods bind a key+action through <c>Hotline.Api.Hud.RegisterHotkey</c>; Hotline owns
    /// the polling (one place, in <see cref="Poll"/>) and flags conflicts when two owners claim the same key. This is
    /// the opt-in half of "stop every mod inventing its own hotkey": a mod declares its key here and also gets a
    /// button in the overlay, so the key is documented, listable and movable instead of hidden in the mod's code.
    /// </summary>
    internal static class HotkeyRegistry
    {
        private static readonly List<HotkeyBinding> _binds = new List<HotkeyBinding>(16);

        internal static IReadOnlyList<HotkeyBinding> All => _binds;

        internal static void Register(string owner, string label, KeyCode key, Action run)
        {
            if (run == null || string.IsNullOrEmpty(label)) return;
            owner = string.IsNullOrEmpty(owner) ? "misc" : owner;

            if (key != KeyCode.None)
            {
                for (int i = 0; i < _binds.Count; i++)
                {
                    HotkeyBinding b = _binds[i];
                    if (b.Key == key && b.Owner != owner)
                        Core.Log?.Warning($"[hotline] hotkey conflict: {key} is bound by both '{b.Owner}' and '{owner}' - both will fire.");
                }
            }

            // replace any existing binding with the same owner+label (deterministic re-registration / rebind)
            _binds.RemoveAll(b => b.Owner == owner && b.Label == label);
            _binds.Add(new HotkeyBinding { Owner = owner, Label = label, Key = key, Run = run });
        }

        /// <summary>Fire any bound action whose key went down this frame. Cheap: a handful of GetKeyDown checks.</summary>
        internal static void Poll()
        {
            for (int i = 0; i < _binds.Count; i++)
            {
                HotkeyBinding b = _binds[i];
                if (b.Key == KeyCode.None) continue;
                if (Input.GetKeyDown(b.Key))
                {
                    try { b.Run?.Invoke(); } catch (Exception e) { Core.Log?.Warning($"[hotline] hotkey '{b.Owner}:{b.Label}' threw: {e.Message}"); }
                }
            }
        }
    }
}
