using System;
using System.Collections.Generic;

namespace Hotline.Panels
{
    /// <summary>A clickable action a mod exposes in its Hotline panel (the in-game replacement for a debug hotkey).
    /// <see cref="Id"/> is the stable handle the console + click handler use to invoke it.</summary>
    internal sealed class ActionItem
    {
        public string Id;
        public string Label;
        public Action Run;
    }

    /// <summary>An on/off control a mod exposes in its Hotline panel (the in-game replacement for a toggle hotkey).</summary>
    internal sealed class ToggleItem
    {
        public string Id;
        public string Label;
        public Func<bool> Get;
        public Action<bool> Set;
    }

    /// <summary>One mod's panel: a named, toggleable window that owns free-text readouts, action buttons and toggles.
    /// Numeric data is expressed through <see cref="Texts"/> providers (a mod renders its own values into text), so
    /// the framework stays domain-agnostic - it is a HUD/hotkey hub, not a metrics store.</summary>
    internal sealed class PanelModel
    {
        public string Id;
        public string Title;
        public bool HasLog;
        public readonly List<Func<string>> Texts = new List<Func<string>>(2);
        public readonly List<ActionItem> Actions = new List<ActionItem>(4);
        public readonly List<ToggleItem> Toggles = new List<ToggleItem>(4);
        // Image providers return {width, height, argb0, argb1, ...} (ARGB32 per pixel) or null/empty for "nothing
        // to show". Kept as a raw int[] so the shim boundary stays Unity-free; the window manager builds the texture.
        public readonly List<Func<int[]>> Images = new List<Func<int[]>>(1);
    }

    /// <summary>
    /// Registry of per-mod panels. Mods register panels/actions/toggles/text through the Hotline.Api shim
    /// (load-order-proof); the HUD window manager reads them back. Action/toggle delegates run on the main thread
    /// (driven from the console or the in-game click handler).
    /// </summary>
    internal static class PanelRegistry
    {
        private static readonly List<PanelModel> _panels = new List<PanelModel>(8);
        private static readonly Dictionary<string, PanelModel> _byId = new Dictionary<string, PanelModel>(8);
        private static readonly Dictionary<string, ActionItem> _actions = new Dictionary<string, ActionItem>(16);
        private static readonly Dictionary<string, ToggleItem> _toggles = new Dictionary<string, ToggleItem>(16);

        internal static IReadOnlyList<PanelModel> All => _panels;
        internal static int Count => _panels.Count;

        internal static PanelModel GetOrCreate(string id, string title)
        {
            if (string.IsNullOrEmpty(id)) id = "misc";
            if (!_byId.TryGetValue(id, out PanelModel p))
            {
                p = new PanelModel { Id = id, Title = string.IsNullOrEmpty(title) ? id : title };
                _byId[id] = p;
                _panels.Add(p);
            }
            else if (!string.IsNullOrEmpty(title))
            {
                p.Title = title;
            }
            return p;
        }

        internal static PanelModel Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            _byId.TryGetValue(id, out PanelModel p);
            return p;
        }

        internal static void RegisterPanel(string id, string title) => GetOrCreate(id, title);

        internal static void RegisterText(string panelId, Func<string> provider)
        {
            if (provider == null) return;
            GetOrCreate(panelId, null).Texts.Add(provider);
        }

        internal static void RegisterAction(string panelId, string actionId, string label, Action run)
        {
            if (run == null || string.IsNullOrEmpty(actionId)) return;
            PanelModel p = GetOrCreate(panelId, null);
            var item = new ActionItem { Id = actionId, Label = label ?? actionId, Run = run };
            // replace any existing action with the same id (deterministic re-registration)
            p.Actions.RemoveAll(a => a.Id == actionId);
            p.Actions.Add(item);
            _actions[actionId] = item;
        }

        internal static void RegisterToggle(string panelId, string toggleId, string label, Func<bool> get, Action<bool> set)
        {
            if (get == null || set == null || string.IsNullOrEmpty(toggleId)) return;
            PanelModel p = GetOrCreate(panelId, null);
            var item = new ToggleItem { Id = toggleId, Label = label ?? toggleId, Get = get, Set = set };
            p.Toggles.RemoveAll(t => t.Id == toggleId);
            p.Toggles.Add(item);
            _toggles[toggleId] = item;
        }

        internal static void RegisterImage(string panelId, Func<int[]> provider)
        {
            if (provider == null) return;
            GetOrCreate(panelId, null).Images.Add(provider);
        }

        internal static void BindPanelLog(string panelId) => GetOrCreate(panelId, null).HasLog = true;

        /// <summary>Invoke an action by id (main thread). Returns false if no such action.</summary>
        internal static bool Invoke(string actionId)
        {
            if (string.IsNullOrEmpty(actionId) || !_actions.TryGetValue(actionId, out ActionItem a)) return false;
            try { a.Run?.Invoke(); } catch (Exception e) { Core.Log?.Warning($"[hotline] action '{actionId}' threw: {e.Message}"); }
            return true;
        }

        internal static bool SetToggle(string toggleId, bool value)
        {
            if (string.IsNullOrEmpty(toggleId) || !_toggles.TryGetValue(toggleId, out ToggleItem t)) return false;
            try { t.Set?.Invoke(value); } catch (Exception e) { Core.Log?.Warning($"[hotline] toggle '{toggleId}' threw: {e.Message}"); }
            return true;
        }

        internal static bool GetToggle(string toggleId)
        {
            if (!string.IsNullOrEmpty(toggleId) && _toggles.TryGetValue(toggleId, out ToggleItem t))
            {
                try { return t.Get != null && t.Get(); } catch { }
            }
            return false;
        }
    }
}
