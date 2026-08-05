using Hash.Api;
using System;
using MelonLoader;
using UnityEngine;
using Hotline.Config;
using Hotline.Hotkeys;
using Hotline.Logging;

[assembly: MelonInfo(typeof(Hotline.Core), "Hotline", DooDesch.ModVersion.Current, "DooDesch", "https://github.com/DooDesch-Mods/ScheduleOne-Hotline")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace Hotline
{
    /// <summary>
    /// MelonLoader entry point for the Hotline framework. It installs the modder API bridge and the console bridge
    /// ("hotline ..."), then each in-world frame drives the one master overlay key, the central hotkey table and the
    /// overlay input. Mods register panels/hotkeys through Hotline.Api; everything stays hidden until you press the
    /// master key (default F7).
    /// </summary>
    public sealed class Core : MelonMod
    {
        public static Core Instance { get; private set; }
        public static MelonLogger.Instance Log { get; private set; }
        internal static HarmonyLib.Harmony HarmonyInst { get; private set; }

        private bool _inWorld;

        public override void OnInitializeMelon()
        {
            Instance = this;
            Log = LoggerInstance;
            HarmonyInst = HarmonyInstance;

            Preferences.Initialize();
            LogHub.Install();

            // Publish the modder API bridge as early as possible so other mods' Hotline.Api calls bind.
            Hotline.Bridge.BridgeHost.Install();

            // Nothing registers `hotline` with the game - the prefix below answers it - so no command list, help
            // overlay or autocomplete can learn the word exists. One call puts it in the game's own list.
            HashCommands.Add("hotline", "overlay: list, open, close, key", "hotline list");

            // The console bridge (Console.SubmitCommand prefixes) is the product's control surface.
            try { HarmonyInstance.PatchAll(); }
            catch (Exception e) { Log.Warning("[Hotline] Harmony patch failed: " + e.Message); }

            Log.Msg("Hotline v" + (MelonAssembly?.Assembly?.GetName()?.Version?.ToString(3) ?? "?")
                    + " - mod HUD & hotkey hub. Press " + Preferences.MasterKey + " in-world to open the overlay ('hotline help' for commands).");
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            _inWorld = sceneName == "Main";
            // Install auto-interception once we are in-world (all mods loaded -> RegisteredMelons is complete).
            // Fail-safe: disables itself if the patches can't install; the game is unaffected either way.
            if (_inWorld) Hotline.Hotkeys.Interceptor.Install(HarmonyInstance);
        }

        public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
        {
            _inWorld = false;
        }

        public override void OnUpdate()
        {
            if (!_inWorld || !Preferences.Enabled) return;
            try
            {
                // The master key is the one always-available entry point. It summons the overlay AND guarantees the
                // Overview window (which hosts the per-panel toggle buttons) is visible - so the overlay can never get
                // stuck closed after the user shuts the Overview's own [x]. Press again (with the Overview up) to dismiss.
                if (Input.GetKeyDown(Preferences.MasterKey))
                {
                    if (!Preferences.ShowHud || !UI.WindowLayout.IsVisible("overview"))
                    {
                        UI.Overlay.Show(true);   // same path the console commands take
                        // if any mod also mapped the master key, reveal its panel so its proxy button is reachable.
                        foreach (string m in Hotline.Hotkeys.Interceptor.ModsForKey(Preferences.MasterKey))
                            UI.WindowLayout.SetVisible(m, true);
                    }
                    else UI.Overlay.Show(false);
                }

                HotkeyRegistry.Poll();   // fire any mod's centrally-bound hotkeys

                if (Preferences.ShowHud) UI.WindowManager.HandleInput();
            }
            catch { /* never let overlay input break the update loop */ }
        }

        public override void OnGUI()
        {
            if (!_inWorld || !Preferences.Enabled || !Preferences.ShowHud) return;
            UI.WindowManager.Draw();
        }

        public override void OnApplicationQuit()
        {
            LogHub.Uninstall();
        }

        public override void OnDeinitializeMelon()
        {
            LogHub.Uninstall();
        }
    }
}
