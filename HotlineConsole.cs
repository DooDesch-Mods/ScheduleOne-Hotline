using System;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using Hotline.Config;
using Hotline.Hotkeys;
using Hotline.Logging;
using Hotline.Panels;
using Hotline.UI;

namespace Hotline
{
    /// <summary>
    /// Console bridge. Patches the game's <c>Console.SubmitCommand</c> (both overloads) and intercepts the
    /// "hotline ..." namespace so the overlay, panels and central hotkeys can be driven from the in-game console
    /// (and headlessly).
    /// </summary>
    internal static class HotlineConsole
    {
        private static int _lastFrame = -1;
        private static string _lastSig = "";

        internal static bool TryHandle(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return false;
            return Dispatch(raw.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries));
        }

        internal static bool TryHandle(Il2CppSystem.Collections.Generic.List<string> args)
        {
            if (args == null || args.Count == 0) return false;
            string[] p = new string[args.Count];
            for (int i = 0; i < args.Count; i++) p[i] = args[i];
            return Dispatch(p);
        }

        private static bool Dispatch(string[] p)
        {
            if (p.Length == 0 || !p[0].Equals("hotline", StringComparison.OrdinalIgnoreCase))
            {
                return false;   // not ours - let the game handle it
            }

            // Both SubmitCommand overloads fire for one entry - dedupe the same command within one frame.
            string sig = string.Join(" ", p);
            int frame = Time.frameCount;
            if (frame == _lastFrame && sig == _lastSig) return true;
            _lastFrame = frame; _lastSig = sig;
            LogHub.Write("Console", 0, sig);

            string cmd = p.Length > 1 ? p[1].ToLowerInvariant() : "panels";
            try
            {
                switch (cmd)
                {
                    case "hud": Hud(p); break;
                    case "panels": PanelsList(); break;
                    case "panel": PanelCmd(p); break;
                    case "act": ActCmd(p); break;
                    case "toggle": ToggleCmd(p); break;
                    case "log": LogCmd(p); break;
                    case "keys": KeysList(); break;
                    case "intercept": Intercept(p); break;
                    case "key": KeyCmd(p); break;
                    case "help": Help(); break;
                    default: Log($"unknown '{cmd}'. Try 'hotline help'."); break;
                }
            }
            catch (Exception e)
            {
                Log("error: " + e.Message);
            }
            return true;
        }

        private static void Help()
        {
            Log("commands: hud [on|off|move <x> <y>|font <n>|reset] | "
                + "panels | panel <id> [on|off|move <x> <y>|size <w> <h>|reset] | "
                + "act <actionId> | toggle <toggleId> [on|off] | log [<channel>|all] [n] | keys | "
                + "intercept [on|off|status|suppress on|off] | key press <KeyCode> [mod] | key master <KeyCode>");
        }

        // auto-interception of other mods' function keys
        private static void Intercept(string[] p)
        {
            string sub = p.Length > 2 ? p[2].ToLowerInvariant() : "status";
            if (sub == "on" || sub == "off")
            {
                Preferences.SetInterceptFunctionKeys(sub == "on");
                MelonPreferences.Save();
                Log("auto-interception = " + Preferences.InterceptFunctionKeys);
                return;
            }
            if (sub == "suppress")
            {
                bool v = BoolArg(p, 3, !Preferences.SuppressRawKeys);
                Preferences.SetSuppressRawKeys(v);
                MelonPreferences.Save();
                Log("suppress raw keys = " + Preferences.SuppressRawKeys);
                return;
            }
            Log($"auto-interception: pref={Preferences.InterceptFunctionKeys} suppressRaw={Preferences.SuppressRawKeys} patch={(Interceptor.Installed ? "installed" : "NOT installed")} postfixCalls={Interceptor.PostfixCalls}");
            var owners = Interceptor.Owners;
            if (owners.Count == 0) { Log("  no mod function keys discovered yet."); return; }
            foreach (var kv in owners) Log($"  {kv.Key} <- {string.Join(", ", kv.Value)}");
        }

        // hotline key press <KeyCode> - queue a synthetic press (the discovered mod gets it on its next poll)
        private static void KeyCmd(string[] p)
        {
            string sub = p.Length > 2 ? p[2].ToLowerInvariant() : null;
            if (sub == "press" && p.Length > 3)
            {
                if (Enum.TryParse(p[3], true, out KeyCode k) && k != KeyCode.None)
                {
                    if (p.Length > 4)
                    {
                        Interceptor.RequestPress(k, p[4]);
                        Log($"queued synthetic press: {k} -> {p[4]}");
                    }
                    else
                    {
                        int n = 0;
                        foreach (string m in Interceptor.ModsForKey(k)) { Interceptor.RequestPress(k, m); n++; }
                        Log(n > 0 ? $"queued synthetic press: {k} -> {n} mod(s)" : $"no mod polls {k} yet (nothing to press).");
                    }
                }
                else Log("unknown key '" + p[3] + "' (use a UnityEngine.KeyCode name, e.g. F9).");
                return;
            }
            if (sub == "master" && p.Length > 3)
            {
                if (Enum.TryParse(p[3], true, out KeyCode k) && k != KeyCode.None)
                {
                    Preferences.SetMasterKey(k.ToString());
                    MelonPreferences.Save();
                    Log("master overlay key = " + Preferences.MasterKey);
                }
                else Log("unknown key '" + p[3] + "' (use a UnityEngine.KeyCode name, e.g. F6).");
                return;
            }
            Log("usage: hotline key press <KeyCode> | hotline key master <KeyCode>");
        }

        // hud on|off (toggle the whole overlay) plus move/font/reset for the overview window.
        private static void Hud(string[] p)
        {
            string sub = p.Length > 2 ? p[2].ToLowerInvariant() : null;
            switch (sub)
            {
                case "move":
                    Preferences.SetHudPos(FloatArg(p, 3, Preferences.HudX), FloatArg(p, 4, Preferences.HudY));
                    WindowLayout.Get("overview", 8f, 8f, 320f, 300f, true);
                    WindowLayout.SetPos("overview", Preferences.HudX, Preferences.HudY);
                    WindowLayout.Save();
                    Log($"overview pos = {Preferences.HudX:F0},{Preferences.HudY:F0}");
                    break;
                case "font":
                    Preferences.SetHudFontSize((int)FloatArg(p, 3, Preferences.HudFontSize));
                    MelonPreferences.Save();
                    Log("overlay font = " + Preferences.HudFontSize);
                    break;
                case "reset":
                    Preferences.SetHudPos(8f, 8f);
                    Preferences.SetHudFontSize(12);
                    WindowLayout.Reset("overview");
                    MelonPreferences.Save();
                    Log("overlay reset (pos 8,8 font 12)");
                    break;
                default:   // null / on / off / true / 1 ... -> show/hide the whole overlay
                    bool v = BoolArg(p, 2, !Preferences.ShowHud);
                    Preferences.SetShowHud(v);
                    if (v) WindowLayout.SetVisible("overview", true);
                    MelonPreferences.Save();
                    Log("overlay = " + Preferences.ShowHud);
                    break;
            }
        }

        private static void PanelsList()
        {
            Log($"overview [{(WindowLayout.IsVisible("overview") ? "on" : "off")}]  timeline [{(WindowLayout.IsVisible("timeline") ? "on" : "off")}]");
            var panels = PanelRegistry.All;
            if (panels.Count == 0) { Log("no mod panels registered yet (enter the world; mods register on load)."); return; }
            for (int i = 0; i < panels.Count; i++)
            {
                PanelModel p = panels[i];
                Log($"  {p.Id,-16} [{(WindowLayout.IsVisible(p.Id) ? "on" : "off")}]  actions={p.Actions.Count} toggles={p.Toggles.Count} title=\"{p.Title}\"");
            }
        }

        // panel <id> [on|off|move <x> <y>|size <w> <h>|reset]. <id> can be a mod panel id, "overview" or "timeline".
        private static void PanelCmd(string[] p)
        {
            if (p.Length <= 2) { Log("usage: hotline panel <id> [on|off|move <x> <y>|size <w> <h>|reset]. 'hotline panels' lists ids."); return; }
            string id = p[2];
            string sub = p.Length > 3 ? p[3].ToLowerInvariant() : "on";
            switch (sub)
            {
                case "on": WindowLayout.SetVisible(id, true); Log($"panel {id} = on"); break;
                case "off": WindowLayout.SetVisible(id, false); Log($"panel {id} = off"); break;
                case "move":
                    WindowLayout.Get(id, 8f, 8f, 320f, 240f, true);
                    WindowLayout.SetPos(id, FloatArg(p, 4, 8f), FloatArg(p, 5, 8f));
                    WindowLayout.Save();
                    Log($"panel {id} moved");
                    break;
                case "size":
                    WindowLayout.Get(id, 8f, 8f, 320f, 240f, true);
                    WindowLayout.SetSize(id, FloatArg(p, 4, 320f), FloatArg(p, 5, 240f));
                    WindowLayout.Save();
                    Log($"panel {id} resized");
                    break;
                case "reset": WindowLayout.Reset(id); Log($"panel {id} reset"); break;
                default: Log("usage: hotline panel <id> [on|off|move <x> <y>|size <w> <h>|reset]"); break;
            }
        }

        private static void ActCmd(string[] p)
        {
            if (p.Length <= 2) { Log("usage: hotline act <actionId> (see the panel; ids look like 'Siesta:force-cosmetic')."); return; }
            Log(PanelRegistry.Invoke(p[2]) ? "ran " + p[2] : "no action '" + p[2] + "'");
        }

        private static void ToggleCmd(string[] p)
        {
            if (p.Length <= 2) { Log("usage: hotline toggle <toggleId> [on|off] (omit to flip)."); return; }
            string id = p[2];
            bool val = BoolArg(p, 3, !PanelRegistry.GetToggle(id));
            Log(PanelRegistry.SetToggle(id, val) ? $"{id} = {val}" : "no toggle '" + id + "'");
        }

        private static void LogCmd(string[] p)
        {
            string ch = p.Length > 2 ? p[2] : "all";
            int n = IntArg(p, 3, 25);
            var entries = (ch.Equals("all", StringComparison.OrdinalIgnoreCase)) ? LogHub.Timeline(n) : LogHub.Channel(ch, n);
            if (entries.Count == 0) { Log($"log '{ch}': no entries (channels: {string.Join(", ", LogHub.Channels())})."); return; }
            Log($"log '{ch}' (last {entries.Count}):");
            foreach (LogEntry e in entries)
            {
                string lv = e.Lvl == 2 ? "E" : (e.Lvl == 1 ? "W" : "I");
                Log($"  {e.Time} {lv} [{e.Ch}] {e.Msg}");
            }
        }

        private static void KeysList()
        {
            var binds = HotkeyRegistry.All;
            if (binds.Count == 0) { Log("no central hotkeys registered. Master overlay key = " + Preferences.MasterKey + "."); return; }
            Log($"central hotkeys (master overlay key = {Preferences.MasterKey}):");
            for (int i = 0; i < binds.Count; i++)
            {
                HotkeyBinding b = binds[i];
                Log($"  {b.Owner}:{b.Label}  =  {(b.Key == KeyCode.None ? "(button only)" : b.Key.ToString())}");
            }
        }

        private static int IntArg(string[] p, int idx, int def)
        {
            if (p.Length > idx && int.TryParse(p[idx], out int v)) return v;
            return def;
        }

        private static float FloatArg(string[] p, int idx, float def)
        {
            if (p.Length > idx && float.TryParse(p[idx], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float v)) return v;
            return def;
        }

        private static bool BoolArg(string[] p, int idx, bool toggleDefault)
        {
            if (p.Length <= idx) return toggleDefault;
            string v = p[idx].ToLowerInvariant();
            if (v == "on" || v == "true" || v == "1" || v == "yes") return true;
            if (v == "off" || v == "false" || v == "0" || v == "no") return false;
            return toggleDefault;
        }

        internal static void Log(string msg)
        {
            Core.Log?.Msg("[hotline] " + msg);
            LogHub.Write("Hotline", 0, msg);
        }
    }

    [HarmonyPatch(typeof(Il2CppScheduleOne.Console), "SubmitCommand", new System.Type[] { typeof(string) })]
    internal static class Hotline_Console_SubmitCommand_String_Patch
    {
        private static bool Prefix(string args)
        {
            try { return !HotlineConsole.TryHandle(args); } catch { return true; }
        }
    }

    [HarmonyPatch(typeof(Il2CppScheduleOne.Console), "SubmitCommand", new System.Type[] { typeof(Il2CppSystem.Collections.Generic.List<string>) })]
    internal static class Hotline_Console_SubmitCommand_List_Patch
    {
        private static bool Prefix(Il2CppSystem.Collections.Generic.List<string> args)
        {
            try { return !HotlineConsole.TryHandle(args); } catch { return true; }
        }
    }
}
