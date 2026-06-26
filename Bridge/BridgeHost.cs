using UnityEngine;
using Hotline.Hotkeys;
using Hotline.Logging;
using Hotline.Panels;

namespace Hotline.Bridge
{
    /// <summary>
    /// Installs the host implementations into <see cref="HotlineBridge"/> so the modder shim (Hotline.Api) lights up.
    /// Panels, logs and hotkeys are all available immediately on registration - a mod's controls exist as soon as it
    /// registers them, so the overlay can show them the moment you summon it.
    /// </summary>
    internal static class BridgeHost
    {
        internal static void Install()
        {
            HotlineBridge.RegisterPanel = (id, title) => PanelRegistry.RegisterPanel(id, title);
            HotlineBridge.RegisterAction = (panelId, actionId, label, run) => PanelRegistry.RegisterAction(panelId, actionId, label, run);
            HotlineBridge.RegisterToggle = (panelId, toggleId, label, get, set) => PanelRegistry.RegisterToggle(panelId, toggleId, label, get, set);
            HotlineBridge.RegisterText = (panelId, provider) => PanelRegistry.RegisterText(panelId, provider);
            HotlineBridge.BindPanelLog = panelId => PanelRegistry.BindPanelLog(panelId);
            HotlineBridge.Log = (channel, level, message) => LogHub.Write(channel, level, message);
            HotlineBridge.RegisterHotkey = (owner, label, key, run) => HotkeyRegistry.Register(owner, label, (KeyCode)key, run);
        }
    }
}
