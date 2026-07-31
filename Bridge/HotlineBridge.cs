using System;

namespace Hotline.Bridge
{
    /// <summary>
    /// The ONE stable contract between the Hotline host and the modder shim (Hotline.Api). The shim locates this
    /// type by full name via reflection and binds these standard-BCL delegates - so the two assemblies share no
    /// custom type and stay version-independent. NEVER rename this type, its namespace, or these fields without
    /// bumping <see cref="AbiVersion"/>; only ADD fields (additive ABI). Filled by <see cref="BridgeHost"/>.
    /// </summary>
    public static class HotlineBridge
    {
        public const int AbiVersion = 1;

        public static Action<string, string> RegisterPanel;                                    // panelId, title
        public static Action<string, string, string, Action> RegisterAction;                   // panelId, actionId, label, run
        public static Action<string, string, string, Func<bool>, Action<bool>> RegisterToggle; // panelId, toggleId, label, get, set
        public static Action<string, Func<string>> RegisterText;                               // panelId, multi-line readout provider
        public static Action<string> BindPanelLog;                                             // panelId -> show its log channel in the panel
        public static Action<string, int, string> Log;                                         // channel, level(0=info,1=warn,2=error), message
        public static Action<string, string, int, Action> RegisterHotkey;                      // ownerId, label, keyCode(UnityEngine.KeyCode int), run
        public static Action<string, Func<int[]>> RegisterImage;                               // panelId, provider -> {w, h, argb...} or null

        // A continuous value with a draggable track. Signature kept to standard BCL types like the rest of the
        // contract, which is why it is a flat parameter list rather than a settings object.
        public static Action<string, string, string, double, double, double, string, Func<double>, Action<double>> RegisterSlider;
        // panelId, sliderId, label, min, max, step (0 = continuous), unit, get, set
    }
}
