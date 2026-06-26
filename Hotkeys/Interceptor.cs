using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using Hotline.Config;
using Hotline.Logging;
using Hotline.Panels;
using Hotline.UI;

namespace Hotline.Hotkeys
{
    /// <summary>
    /// Auto-interception layer. It DISCOVERS which function keys other mods poll, attributes each to the polling mod(s),
    /// surfaces it as a proxy button in a per-mod auto-panel, and INJECTS a one-frame synthetic press (precisely to that
    /// mod) when the user clicks - so even mods that never adopt Hotline get their hotkeys onto the unified overlay.
    ///
    /// Attribution is IL2CPP-robust: a managed stack walk from a Harmony postfix on a Unity method does NOT contain the
    /// il2cpp-side mod caller, so instead Hotline wraps every OTHER mod's lifecycle methods with a prefix that records
    /// "this mod is executing now" in a thread-static marker; the postfix on <c>Input.GetKeyDown(KeyCode)</c> reads it.
    ///
    /// Two takeover behaviours:
    ///  - the MASTER key (Hotline's own overlay key): always taken away from mods (so it can never double-fire with the
    ///    overlay toggle), but the mod still gets a proxy button and its panel is revealed when the overlay opens;
    ///  - any OTHER key: only taken over when <see cref="Preferences.SuppressRawKeys"/> is ON ("full takeover"); off by
    ///    default, where the raw key keeps working and the proxy button is just an additive convenience.
    ///
    /// Vanilla is safe by construction (the marker is only set while a tracked MOD's OnUpdate runs - never the game's
    /// own polls), and a synthetic press can't desync (input is client-local). Fail-safe if the patches can't install.
    /// </summary>
    internal static class Interceptor
    {
        private static bool _installed;
        private static bool _failed;
        private static readonly Assembly _selfAsm = typeof(Interceptor).Assembly;

        // the mod whose OnUpdate/OnLateUpdate/OnFixedUpdate is executing right now (input polling is main-thread)
        [ThreadStatic] private static string _currentOwner;

        private static readonly Dictionary<MethodBase, string> _ownerOf = new Dictionary<MethodBase, string>();
        // key -> the set of mods that poll it (each (key, mod) pair gets its own proxy button)
        private static readonly Dictionary<KeyCode, HashSet<string>> _owners = new Dictionary<KeyCode, HashSet<string>>();
        // precise pending synthetic presses, per (key, mod) -> frame requested (consumed by that mod's next poll; expires)
        private static readonly Dictionary<(KeyCode key, string mod), int> _inject = new Dictionary<(KeyCode, string), int>();

        internal static long PostfixCalls;
        internal static bool Installed => _installed && !_failed;
        internal static IReadOnlyDictionary<KeyCode, HashSet<string>> Owners => _owners;

        private static readonly KeyCode[] Targets =
        {
            KeyCode.F1, KeyCode.F2, KeyCode.F3, KeyCode.F4, KeyCode.F5, KeyCode.F6,
            KeyCode.F7, KeyCode.F8, KeyCode.F9, KeyCode.F10, KeyCode.F11, KeyCode.F12
        };
        private static readonly string[] Lifecycle = { "OnUpdate", "OnLateUpdate", "OnFixedUpdate" };

        private static bool IsTarget(KeyCode k)
        {
            for (int i = 0; i < Targets.Length; i++) if (Targets[i] == k) return true;
            return false;
        }

        /// <summary>Install both patches. Idempotent. Call once all mods are loaded (e.g. on first world load) so
        /// <see cref="MelonMod.RegisteredMelons"/> is complete.</summary>
        internal static void Install(HarmonyLib.Harmony h)
        {
            if (_installed || _failed || h == null) return;
            try
            {
                MethodInfo gk = typeof(Input).GetMethod("GetKeyDown", new[] { typeof(KeyCode) });
                if (gk == null)
                {
                    _failed = true;
                    Core.Log?.Warning("[hotline] interceptor: Input.GetKeyDown(KeyCode) not found - auto-interception disabled.");
                    return;
                }
                h.Patch(gk, postfix: new HarmonyMethod(AccessTools.Method(typeof(Interceptor), nameof(GetKeyDownPostfix))));

                MethodInfo pre = AccessTools.Method(typeof(Interceptor), nameof(OwnerPre));
                MethodInfo fin = AccessTools.Method(typeof(Interceptor), nameof(OwnerFin));
                foreach (MelonMod mod in RegisteredMods())
                {
                    if (mod == null) continue;
                    Type t = mod.GetType();
                    if (t.Assembly == _selfAsm) continue;   // never wrap Hotline itself
                    string name = NameOf(mod);
                    for (int i = 0; i < Lifecycle.Length; i++)
                    {
                        MethodInfo mi = t.GetMethod(Lifecycle[i],
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                            null, Type.EmptyTypes, null);
                        if (mi == null || mi.IsAbstract || mi.DeclaringType == typeof(MelonMod) || _ownerOf.ContainsKey(mi)) continue;
                        try
                        {
                            h.Patch(mi, prefix: new HarmonyMethod(pre), finalizer: new HarmonyMethod(fin));
                            _ownerOf[mi] = name;
                        }
                        catch (Exception e) { Core.Log?.Warning($"[hotline] wrap {name}.{Lifecycle[i]} failed: {e.Message}"); }
                    }
                }

                _installed = true;
                Core.Log?.Msg($"[hotline] auto-interception installed (Input.GetKeyDown + {_ownerOf.Count} mod lifecycle hooks).");
            }
            catch (Exception e)
            {
                _failed = true;
                Core.Log?.Warning("[hotline] interceptor patch failed (auto-interception disabled, game unaffected): " + e.Message);
            }
        }

        // ----- lifecycle owner tracking (prefix/finalizer wrapped onto each mod's OnUpdate etc.) -----

        private static void OwnerPre(MethodBase __originalMethod, out string __state)
        {
            __state = _currentOwner;   // save (handles the rare case of a mod calling into another mod)
            _ownerOf.TryGetValue(__originalMethod, out string name);
            _currentOwner = name;
        }

        private static void OwnerFin(string __state) { _currentOwner = __state; }

        // ----- the input read postfix -----

        // __0 = the KeyCode arg (by position, survives IL2CPP parameter-name stripping); __result = the engine's bool.
        private static void GetKeyDownPostfix(KeyCode __0, ref bool __result)
        {
            try
            {
                if (_failed || !Preferences.InterceptFunctionKeys || !IsTarget(__0)) return;
                PostfixCalls++;

                string mod = _currentOwner;
                if (string.IsNullOrEmpty(mod)) return;   // not inside a tracked mod's OnUpdate (vanilla / Hotline) -> ignore

                // discovery: each (key, mod) pair gets a proxy button in that mod's panel.
                if (!_owners.TryGetValue(__0, out HashSet<string> set)) { set = new HashSet<string>(); _owners[__0] = set; }
                if (set.Add(mod)) Discovered(mod, __0);

                // precise injection: a queued synthetic press for THIS (key, mod) wins - feed it to the mod once.
                var ik = (__0, mod);
                if (_inject.TryGetValue(ik, out int reqFrame))
                {
                    if (Time.frameCount - reqFrame > 20) _inject.Remove(ik);   // expired, never consumed
                    else { __result = true; _inject.Remove(ik); return; }
                }

                if (!__result) return;   // not a real physical press this frame

                if (__0 == Preferences.MasterKey)
                {
                    // The master key belongs to Hotline: never let a mod fire it directly (that would double-fire with the
                    // overlay toggle). The overlay - and this mod's panel - are opened by Core's master-key handler; here
                    // we just hide the raw key from the mod. Its action stays reachable via its proxy button.
                    __result = false;
                }
                else if (Preferences.SuppressRawKeys)
                {
                    // full takeover (opt-in): open our overlay on this mod's panel and hide the raw key.
                    Summon(mod);
                    __result = false;
                }
            }
            catch { /* never break a hot input path */ }
        }

        /// <summary>Queue a one-frame synthetic press of <paramref name="key"/> for exactly <paramref name="mod"/>. The
        /// next time that mod polls GetKeyDown(key) it gets true (once). Called from the mod's proxy button / console.</summary>
        internal static void RequestPress(KeyCode key, string mod)
        {
            if (key == KeyCode.None || string.IsNullOrEmpty(mod)) return;
            _inject[(key, mod)] = Time.frameCount;
            Core.Log?.Msg($"[hotline] synthetic press queued: {key} -> {mod}");
            LogHub.Write("Hotline", 0, $"press {key} ({mod})");
        }

        /// <summary>The mods that poll <paramref name="key"/> - used by Core to reveal their panels when the master key
        /// opens the overlay.</summary>
        internal static IEnumerable<string> ModsForKey(KeyCode key)
            => _owners.TryGetValue(key, out HashSet<string> set) ? set : (IEnumerable<string>)Array.Empty<string>();

        private static void Discovered(string mod, KeyCode key)
        {
            // Use the mod's name as the panel id (not a "Keys-" prefix) so the button lands IN the mod's own panel when
            // it already registered one under the same id (via Hotline.Api, or forwarded from Snitch.Api) - one window
            // per mod, not a separate hotkeys window. Falls back to a panel named after the mod when it has none. Pass a
            // null title so an existing panel keeps its own.
            PanelRegistry.RegisterPanel(mod, null);
            string actionId = mod + ":key-" + key;
            PanelRegistry.RegisterAction(mod, actionId, "Press " + key, () => RequestPress(key, mod));
            Core.Log?.Msg($"[hotline] discovered hotkey {key} polled by '{mod}' -> added a button to its overlay panel.");
            LogHub.Write("Hotline", 0, $"discovered {key} ({mod})");
        }

        private static int _lastSummonFrame = -1;

        /// <summary>A claimed function key was physically pressed (full-takeover mode) - reveal the Hotline overlay
        /// focused on the owning mod's panel. Once per frame (also avoids redundant preference saves).</summary>
        private static void Summon(string mod)
        {
            if (Time.frameCount == _lastSummonFrame) return;
            _lastSummonFrame = Time.frameCount;
            try
            {
                Preferences.SetShowHud(true);
                WindowLayout.SetVisible("overview", true);
                WindowLayout.SetVisible(mod, true);
            }
            catch { }
        }

        private static string NameOf(MelonMod mod)
        {
            try { string n = mod.Info?.Name; if (!string.IsNullOrEmpty(n)) return n; } catch { }
            return mod.GetType().Namespace ?? mod.GetType().Name;
        }

        private static IEnumerable<MelonMod> RegisteredMods()
        {
            try { if (MelonMod.RegisteredMelons != null) return MelonMod.RegisteredMelons; } catch { }
            foreach (Type t in new[] { typeof(MelonMod), typeof(MelonBase) })
            {
                try
                {
                    PropertyInfo p = t.GetProperty("RegisteredMelons", BindingFlags.Public | BindingFlags.Static);
                    if (p?.GetValue(null) is IEnumerable<MelonMod> mods) return mods;
                }
                catch { }
            }
            return Array.Empty<MelonMod>();
        }
    }
}
