// Copyright (c) 2026 DooDesch. DooDesch API License (see LICENSE.md): you may copy this file unchanged.
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Hotline.Api
{
    /// <summary>
    /// The Hotline framework's modder API. Reference Hotline.Api.dll OR drop this single file into your mod.
    /// Register a panel and you instantly get a ready-made, toggleable, draggable in-game window inside the one
    /// unified Hotline overlay - no custom hotkey, no custom uGUI, no per-mod debug window. One master key opens
    /// the overlay; every mod's controls live there as buttons. Optionally bind a central hotkey to an action and
    /// Hotline owns the polling and conflict detection for you.
    ///
    /// Every call is a zero-overhead no-op when Hotline is not installed and lights up automatically when it is,
    /// so you can ship this unconditionally with no hard dependency.
    ///
    /// <code>
    ///   using Hotline.Api;
    ///   Hud.RegisterPanel("MyMod", "My Mod")
    ///      .Text(() =&gt; "queue = " + _queue.Count)
    ///      .Action("Reload", Reload)
    ///      .Toggle("Verbose", () =&gt; _verbose, v =&gt; _verbose = v)
    ///      .Hotkey("Reload", HotlineKey.F8, Reload);   // optional central hotkey
    /// </code>
    ///
    /// Tip: a class named <c>HotlineProbe</c> with a static <c>Register()</c> is auto-discovered and called on bind
    /// (see <see cref="AutoRegister"/>), so your mod's Core does not have to wire anything.
    ///
    /// All calls MUST be made from the Unity main thread.
    /// </summary>
    /// <remarks>
    /// A panel id is any string you pick, usually the mod name, e.g. "MyMod". It is also the panel's log channel and the
    /// owner of its hotkeys. Each button, toggle and slider gets the id <c>panelId:label-slug</c>: the label in lower case,
    /// with every run of other characters turned into "-". "Say hi" in the panel "HotlineExample" becomes
    /// <c>HotlineExample:say-hi</c>. The console commands <c>hotline act</c>, <c>hotline toggle</c> and <c>hotline slider</c>
    /// take that id. A second control with the same label in the same panel replaces the first.
    /// </remarks>
    /// <example>
    /// A slider, and the panel shown from code:
    /// <code>
    /// Hud.RegisterPanel("MyMod", "My Mod")
    ///    .Slider("Spawn radius", 5, 50, () =&gt; _radius, v =&gt; _radius = v, 1, "m");
    /// Hud.ShowPanel("MyMod", true);
    /// </code>
    /// </example>
    public static class Hud
    {
        // bound bridge delegates (null until the host is found)
        private static bool _bound;
        private static bool _autoDone;
        private static int _probeAttempts;
        private static readonly List<Action> _pending = new List<Action>();

        private static Action<string, string> _registerPanel;
        private static Action<string, string, string, Action> _registerAction;
        private static Action<string, string, string, Func<bool>, Action<bool>> _registerToggle;
        private static Action<string, Func<string>> _registerText;
        private static Action<string, Func<int[]>> _registerImage;
        private static Action<string, string, string, double, double, double, string, Func<double>, Action<double>> _registerSlider;
        private static Action<string> _bindPanelLog;
        private static Action<string, int, string> _log;
        private static Action<string, string, int, Action> _registerHotkey;
        // Overlay control (added after the initial ABI; absent on an older host, where these stay null and the
        // matching public methods are simply no-ops).
        private static Action<bool> _showOverlay;
        private static Action<string, bool> _showPanel;
        private static Func<string, bool> _isPanelVisible;

        /// <summary>True only when the Hotline host is installed AND bound. You rarely need this - the API is a safe
        /// no-op when absent.</summary>
        public static bool Available { get { EnsureBound(); return _bound; } }

        /// <summary>Declare a panel: a named, toggleable, movable window in the Hotline overlay that groups everything
        /// this mod exposes (text readouts, action buttons, toggles, a log channel). Returns a fluent builder.
        /// Load-order-proof; a no-op (the builder still works) if Hotline is absent.</summary>
        /// <param name="id">Your own panel id, e.g. "MyMod". Null or empty registers nothing, and the returned builder does nothing either.</param>
        /// <param name="title">The window title, e.g. "My Mod". Null or empty shows the id. The same id again with a title renames the panel.</param>
        public static Panel RegisterPanel(string id, string title = null)
        {
            var panel = new Panel(id);
            if (string.IsNullOrEmpty(id)) return panel;
            string t = title;
            EnsureBound();
            if (_registerPanel != null) _registerPanel(id, t);
            else _pending.Add(() => _registerPanel?.Invoke(id, t));
            return panel;
        }

        /// <summary>A clickable button in a mod's panel - the in-game replacement for a debug hotkey action. Runs on
        /// the main thread when clicked. Load-order-proof.</summary>
        /// <param name="panelId">The id passed to <see cref="RegisterPanel"/>. An unknown id creates a panel titled with that id; null or empty uses the panel "misc".</param>
        /// <param name="label">The button text, e.g. "Reload". It also forms the action id, e.g. <c>MyMod:reload</c>. Null or empty registers nothing.</param>
        /// <param name="run">Runs on each click or <c>hotline act</c> call. An exception is caught and logged as a warning. Null registers nothing.</param>
        public static void RegisterAction(string panelId, string label, Action run)
        {
            if (run == null || string.IsNullOrEmpty(label)) return;
            string actionId = panelId + ":" + Slug(label);
            EnsureBound();
            if (_registerAction != null) _registerAction(panelId, actionId, label, run);
            else _pending.Add(() => _registerAction?.Invoke(panelId, actionId, label, run));
        }

        /// <summary>An on/off control in a mod's panel - the in-game replacement for a toggle hotkey. <paramref name="get"/>
        /// reports the current state, <paramref name="set"/> applies it; both run on the main thread. Load-order-proof.</summary>
        /// <param name="panelId">The id passed to <see cref="RegisterPanel"/>.</param>
        /// <param name="label">The control text, e.g. "Verbose". It also forms the toggle id, e.g. <c>MyMod:verbose</c>. Null or empty registers nothing.</param>
        /// <param name="get">Returns the current state, e.g. <c>() =&gt; _verbose</c>. Null registers nothing.</param>
        /// <param name="set">Applies the new state, e.g. <c>v =&gt; _verbose = v</c>. Null registers nothing.</param>
        public static void RegisterToggle(string panelId, string label, Func<bool> get, Action<bool> set)
        {
            if (get == null || set == null || string.IsNullOrEmpty(label)) return;
            string toggleId = panelId + ":" + Slug(label);
            EnsureBound();
            if (_registerToggle != null) _registerToggle(panelId, toggleId, label, get, set);
            else _pending.Add(() => _registerToggle?.Invoke(panelId, toggleId, label, get, set));
        }

        /// <summary>
        /// A draggable value in a mod's panel - the in-game replacement for typing a number into a console to find it.
        /// <paramref name="get"/> reports the current value, <paramref name="set"/> applies it; both run on the main
        /// thread. The host clamps to [<paramref name="min"/>, <paramref name="max"/>] and snaps to
        /// <paramref name="step"/> (0 = continuous), so a setter never sees a value outside its range.
        /// Load-order-proof; a no-op on an older Hotline host that predates slider support.
        /// </summary>
        /// <param name="panelId">The id passed to <see cref="RegisterPanel"/>.</param>
        /// <param name="label">The text before the value, e.g. "Spawn radius". It also forms the slider id, e.g. <c>MyMod:spawn-radius</c>. Null or empty registers nothing.</param>
        /// <param name="min">The lowest value, e.g. 5.</param>
        /// <param name="max">The highest value, e.g. 50. A value that is not above <paramref name="min"/> registers nothing.</param>
        /// <param name="get">Returns the current value. Null registers nothing.</param>
        /// <param name="set">Applies a value that is already clamped and snapped. Null registers nothing.</param>
        /// <param name="step">The snap size, e.g. 0.05. 0 or less is continuous. It also sets the decimals shown: up to 2 for 0 or a step of 1 and more, 1 from 0.1, 2 from 0.01, 3 below that.</param>
        /// <param name="unit">Text shown after the value, e.g. "m" or "%". Null or empty shows none.</param>
        public static void RegisterSlider(string panelId, string label, double min, double max,
                                          Func<double> get, Action<double> set, double step = 0d, string unit = null)
        {
            if (get == null || set == null || string.IsNullOrEmpty(label) || max <= min) return;
            string sliderId = panelId + ":" + Slug(label);
            string u = unit ?? "";
            EnsureBound();
            if (_registerSlider != null) _registerSlider(panelId, sliderId, label, min, max, step, u, get, set);
            else _pending.Add(() => _registerSlider?.Invoke(panelId, sliderId, label, min, max, step, u, get, set));
        }

        /// <summary>A free-text, multi-line readout in a mod's panel. Polled by the host on the main thread. Load-order-proof.</summary>
        /// <param name="panelId">The id passed to <see cref="RegisterPanel"/>.</param>
        /// <param name="provider">Returns the text, e.g. <c>() =&gt; "queue = " + _queue.Count</c>. Each call adds one more readout. Null registers nothing.</param>
        public static void RegisterText(string panelId, Func<string> provider)
        {
            if (provider == null) return;
            EnsureBound();
            if (_registerText != null) _registerText(panelId, provider);
            else _pending.Add(() => _registerText?.Invoke(panelId, provider));
        }

        /// <summary>An image in a mod's panel (e.g. a QR code). The provider returns <c>{ width, height, argb0, argb1, ... }</c>
        /// - ARGB32 pixels, row-major top-down - or null/empty for "nothing to show". Polled by the host on the main
        /// thread and drawn Point-filtered. Kept as a raw int[] so this shim needs no Unity reference. Load-order-proof;
        /// a no-op on an older Hotline host that predates image support.</summary>
        /// <param name="panelId">The id passed to <see cref="RegisterPanel"/>.</param>
        /// <param name="provider">
        /// Returns <c>{ width, height, pixel0, pixel1, ... }</c> with width times height pixels after the two sizes. Each pixel
        /// is <c>0xAARRGGBB</c>, so opaque black is <c>unchecked((int)0xFF000000)</c>. A shorter array, or a width or height
        /// of 0 or less, shows nothing. Null registers nothing.
        /// </param>
        /// <example>
        /// A 2 x 2 checkerboard:
        /// <code>
        /// int b = unchecked((int)0xFF000000), w = unchecked((int)0xFFFFFFFF);
        /// Hud.RegisterImage("MyMod", () =&gt; new[] { 2, 2, b, w, w, b });
        /// </code>
        /// </example>
        public static void RegisterImage(string panelId, Func<int[]> provider)
        {
            if (provider == null) return;
            EnsureBound();
            if (_registerImage != null) _registerImage(panelId, provider);
            else _pending.Add(() => _registerImage?.Invoke(panelId, provider));
        }

        /// <summary>Mark that a panel should display its own log channel (the lines you send via <see cref="Log"/> /
        /// <see cref="Panel.Write"/> with the same id). Load-order-proof.</summary>
        /// <param name="panelId">The id passed to <see cref="RegisterPanel"/>. The panel shows the log channel of the same name.</param>
        public static void BindPanelLog(string panelId)
        {
            EnsureBound();
            if (_bindPanelLog != null) _bindPanelLog(panelId);
            else _pending.Add(() => _bindPanelLog?.Invoke(panelId));
        }

        /// <summary>Send a log line to a channel (use your mod/panel id as the channel). It appears in that mod's panel
        /// log AND in Hotline's combined timeline. Load-order-proof; a no-op if Hotline is absent.</summary>
        /// <param name="channel">Your panel id, e.g. "MyMod". Null or empty goes to the channel "misc". A channel keeps its last 400 lines, the combined timeline its last 1200.</param>
        /// <param name="message">The line to show. Null or empty sends nothing.</param>
        /// <param name="level">Info, Warning or Error. The default is Info.</param>
        public static void Log(string channel, string message, LogLevel level = LogLevel.Info)
        {
            if (string.IsNullOrEmpty(message)) return;
            int lv = (int)level;
            EnsureBound();
            if (_log != null) _log(channel, lv, message);
            else _pending.Add(() => _log?.Invoke(channel, lv, message));
        }

        /// <summary>Bind a central hotkey to an action. Hotline owns the key polling and conflict detection, so every
        /// mod's hotkeys live in one place. Pass <see cref="HotlineKey.None"/> to skip the key and only label the
        /// action. The same action typically also appears as a panel button. Load-order-proof.</summary>
        /// <param name="ownerId">Your panel id, e.g. "MyMod". Null or empty uses "misc".</param>
        /// <param name="label">Names the binding, e.g. "Reload". The same owner and label again replaces the binding. Null or empty registers nothing.</param>
        /// <param name="key">One of F1 to F12, or <see cref="HotlineKey.None"/>. When two owners bind the same key, both fire and Hotline logs a warning. F6 is Hotline's default master key.</param>
        /// <param name="run">Runs when the key goes down. An exception is caught and logged as a warning. Null registers nothing.</param>
        public static void RegisterHotkey(string ownerId, string label, HotlineKey key, Action run)
        {
            if (run == null || string.IsNullOrEmpty(label)) return;
            int k = (int)key;
            EnsureBound();
            if (_registerHotkey != null) _registerHotkey(ownerId, label, k, run);
            else _pending.Add(() => _registerHotkey?.Invoke(ownerId, label, k, run));
        }

        /// <summary>Raise or dismiss the whole overlay from code - the same thing the master key does. Lets a mod
        /// offer a console command for its HUD instead of relying on a keypress, which nothing can automate or
        /// verify. No-op when Hotline is absent or has not bound yet (this one is NOT queued: showing an overlay
        /// minutes later, once the host finally turns up, would be a surprise rather than a favour).</summary>
        /// <param name="show">true raises the overlay, false hides it.</param>
        public static void ShowOverlay(bool show)
        {
            EnsureBound();
            _showOverlay?.Invoke(show);
        }

        /// <summary>Show or hide one panel by id. Showing it raises the overlay too, so a single call is enough to
        /// put a mod's panel in front of the player. No-op when Hotline is absent.</summary>
        /// <param name="panelId">The id passed to <see cref="RegisterPanel"/>, e.g. "MyMod". Null or empty does nothing.</param>
        /// <param name="show">true shows the panel and raises the overlay, false hides the panel.</param>
        public static void ShowPanel(string panelId, bool show)
        {
            if (string.IsNullOrEmpty(panelId)) return;
            EnsureBound();
            _showPanel?.Invoke(panelId, show);
        }

        /// <summary>Whether a panel is on screen right now (false when Hotline is absent, or the overlay is down).</summary>
        /// <param name="panelId">The id passed to <see cref="RegisterPanel"/>. Null or empty returns false.</param>
        public static bool IsPanelVisible(string panelId)
        {
            if (string.IsNullOrEmpty(panelId)) return false;
            EnsureBound();
            try { return _isPanelVisible != null && _isPanelVisible(panelId); } catch { return false; }
        }

        /// <summary>Discover a convention type named <c>HotlineProbe</c> with a static <c>Register()</c> in THIS mod's
        /// own assembly and invoke it once - so a mod never has to wire a Register() call into its Core. Drive it from
        /// a <c>[ModuleInitializer]</c> in your probe file. No-op + load-order-proof.</summary>
        public static void AutoRegister()
        {
            EnsureBound();
            if (_bound) RunAutoRegister();   // else: the bind flush will run it (load-order-proof, both directions)
        }

        private static void RunAutoRegister()
        {
            if (_autoDone) return;
            _autoDone = true;   // latch before invoking so a throw can't loop
            try
            {
                Assembly self = typeof(Hud).Assembly;   // only this mod's assembly - single, fast, no AppDomain scan
                Type probe = self.GetType("HotlineProbe", false) ?? FindByLeafName(self, "HotlineProbe");
                MethodInfo reg = probe?.GetMethod("Register",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, Type.EmptyTypes, null);
                reg?.Invoke(null, null);
            }
            catch { /* a mod's probe threw -> stays a no-op, never crashes the mod */ }
        }

        private static Type FindByLeafName(Assembly asm, string leaf)
        {
            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types; }
            catch { return null; }
            if (types == null) return null;
            foreach (Type t in types)
                if (t != null && t.IsClass && t.IsAbstract && t.IsSealed && t.Name == leaf) return t;   // static class
            return null;
        }

        private static string Slug(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
                else if (sb.Length > 0 && sb[sb.Length - 1] != '-') sb.Append('-');
            }
            return sb.ToString().Trim('-');
        }

        // ----- reflection handshake (runs until it binds, then latches) -----

        private static void EnsureBound()
        {
            if (_bound) return;   // bound once, never probe again (fast path)
            try
            {
                Type t = FindBridge((_probeAttempts++ % 30) == 0);
                if (t == null) return;   // host not present yet - cheap re-probe next call (load-order proof)
                object abi = t.GetField("AbiVersion", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (abi is int v && v < 1) return;

                _registerPanel = Get<Action<string, string>>(t, "RegisterPanel");
                _registerAction = Get<Action<string, string, string, Action>>(t, "RegisterAction");
                _registerToggle = Get<Action<string, string, string, Func<bool>, Action<bool>>>(t, "RegisterToggle");
                _registerText = Get<Action<string, Func<string>>>(t, "RegisterText");
                _registerImage = Get<Action<string, Func<int[]>>>(t, "RegisterImage");
                _registerSlider = Get<Action<string, string, string, double, double, double, string, Func<double>, Action<double>>>(t, "RegisterSlider");
                _bindPanelLog = Get<Action<string>>(t, "BindPanelLog");
                _log = Get<Action<string, int, string>>(t, "Log");
                _registerHotkey = Get<Action<string, string, int, Action>>(t, "RegisterHotkey");
                _showOverlay = Get<Action<bool>>(t, "ShowOverlay");
                _showPanel = Get<Action<string, bool>>(t, "ShowPanel");
                _isPanelVisible = Get<Func<string, bool>>(t, "IsPanelVisible");

                if (_registerPanel == null) return;   // partial table - try again next call
                _bound = true;

                // flush any registrations made before the host was up
                for (int i = 0; i < _pending.Count; i++) { try { _pending[i](); } catch { } }
                _pending.Clear();
                RunAutoRegister();
            }
            catch { /* any failure -> stays a no-op, retries next call */ }
        }

        private static T Get<T>(Type t, string field) where T : class
        {
            object v = t.GetField(field, BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            return v as T;   // works because Func<>/Action<> are shared BCL types in both assemblies
        }

        private static Type FindBridge(bool scan)
        {
            Type t = Type.GetType("Hotline.Bridge.HotlineBridge, Hotline", false);
            if (t != null || !scan) return t;
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { t = asm.GetType("Hotline.Bridge.HotlineBridge", false); if (t != null) return t; }
                catch { }
            }
            return null;
        }
    }

    /// <summary>Severity for <see cref="Hud.Log"/>. Mirrors the host's 0=info / 1=warning / 2=error.</summary>
    public enum LogLevel { Info = 0, Warning = 1, Error = 2 }

    /// <summary>Keys Hotline can bind centrally. The integer values match <c>UnityEngine.KeyCode</c> so the host
    /// casts them straight back - the shim stays Unity-free.</summary>
    public enum HotlineKey
    {
        None = 0,
        F1 = 282, F2 = 283, F3 = 284, F4 = 285, F5 = 286, F6 = 287,
        F7 = 288, F8 = 289, F9 = 290, F10 = 291, F11 = 292, F12 = 293
    }

    /// <summary>
    /// Fluent builder for a mod's Hotline panel (returned by <see cref="Hud.RegisterPanel"/>). Everything is a safe
    /// no-op when the Hotline host is absent.
    /// </summary>
    /// <example>
    /// <code>
    /// Panel panel = Hud.RegisterPanel("MyMod", "My Mod").Log();
    /// panel.Write("reloaded", LogLevel.Warning);
    /// </code>
    /// </example>
    public sealed class Panel
    {
        private readonly string _id;
        internal Panel(string id) { _id = id ?? ""; }

        /// <summary>The panel id (also the default log channel).</summary>
        public string Id => _id;

        /// <summary>A free-text, multi-line readout in this panel.</summary>
        public Panel Text(Func<string> provider) { Hud.RegisterText(_id, provider); return this; }

        /// <summary>An image in this panel (e.g. a QR code). Provider returns { width, height, argb... } or null.</summary>
        /// <param name="provider">Returns <c>{ width, height, 0xAARRGGBB pixels... }</c>; see <see cref="Hud.RegisterImage"/>.</param>
        public Panel Image(Func<int[]> provider) { Hud.RegisterImage(_id, provider); return this; }

        /// <summary>A clickable button (replaces a debug hotkey action).</summary>
        /// <param name="label">The button text, e.g. "Reload". It also forms the action id, e.g. <c>MyMod:reload</c>.</param>
        /// <param name="run">Runs on each click or <c>hotline act</c> call.</param>
        public Panel Action(string label, Action run) { Hud.RegisterAction(_id, label, run); return this; }

        /// <summary>An on/off control (replaces a debug toggle hotkey).</summary>
        /// <param name="label">The control text, e.g. "Verbose". It also forms the toggle id, e.g. <c>MyMod:verbose</c>.</param>
        /// <param name="get">Returns the current state.</param>
        /// <param name="set">Applies the new state.</param>
        public Panel Toggle(string label, Func<bool> get, Action<bool> set) { Hud.RegisterToggle(_id, label, get, set); return this; }

        /// <summary>A draggable value (replaces typing a number into a console to find it).</summary>
        /// <param name="label">The text before the value. It also forms the slider id, e.g. <c>MyMod:spawn-radius</c>.</param>
        /// <param name="min">The lowest value.</param>
        /// <param name="max">The highest value. It must be above <paramref name="min"/>.</param>
        /// <param name="get">Returns the current value.</param>
        /// <param name="set">Applies a value that is already clamped and snapped.</param>
        /// <param name="step">The snap size, e.g. 0.05. 0 is continuous. See <see cref="Hud.RegisterSlider"/>.</param>
        /// <param name="unit">Text shown after the value, e.g. "m". Null shows none.</param>
        public Panel Slider(string label, double min, double max, Func<double> get, Action<double> set, double step = 0d, string unit = null)
        { Hud.RegisterSlider(_id, label, min, max, get, set, step, unit); return this; }

        /// <summary>Bind a central hotkey to an action owned by this panel.</summary>
        /// <param name="label">Names the binding, e.g. "Reload". The same label again replaces it.</param>
        /// <param name="key">One of F1 to F12, or <see cref="HotlineKey.None"/> for a label without a key.</param>
        /// <param name="run">Runs when the key goes down.</param>
        public Panel Hotkey(string label, HotlineKey key, Action run) { Hud.RegisterHotkey(_id, label, key, run); return this; }

        /// <summary>Show this panel's own log channel inside the panel.</summary>
        public Panel Log() { Hud.BindPanelLog(_id); return this; }

        /// <summary>Send a log line to this panel's channel (and the combined timeline).</summary>
        public void Write(string message, LogLevel level = LogLevel.Info) { Hud.Log(_id, message, level); }
    }
}
