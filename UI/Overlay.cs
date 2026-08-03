using Hotline.Config;

namespace Hotline.UI
{
    /// <summary>
    /// Showing and hiding the overlay from code, so it is not only reachable by pressing the master key.
    /// <para>
    /// A keypress cannot be scripted, automated or checked without a person at the keyboard, which makes a
    /// key-only overlay impossible to verify and awkward for anyone who wants a console command for their own
    /// panel. Both the master key and <c>hotline</c>/<c>snitch</c> console commands funnel through here, so the
    /// two entry points can never drift apart.
    /// </para>
    /// </summary>
    internal static class Overlay
    {
        /// <summary>The panel list itself. Kept visible whenever the overlay is up, because it hosts the buttons
        /// that toggle every other panel - without it the overlay can be open with no way to reach anything.</summary>
        private const string OverviewId = "overview";

        internal static bool IsOpen => Preferences.ShowHud;

        /// <summary>Raise or dismiss the whole overlay. Raising also restores the Overview, matching what the
        /// master key does, so the overlay can never come up with nothing on it.</summary>
        internal static void Show(bool show)
        {
            Preferences.SetShowHud(show);
            if (show) WindowLayout.SetVisible(OverviewId, true);
        }

        /// <summary>Show or hide one mod's panel. Showing raises the overlay too - asking for a panel while the
        /// overlay is down obviously means "let me see it", not "mark it visible behind a closed overlay".</summary>
        internal static void ShowPanel(string panelId, bool show)
        {
            if (string.IsNullOrEmpty(panelId)) return;
            if (show) Show(true);
            WindowLayout.SetVisible(panelId, show);
        }

        internal static bool IsPanelVisible(string panelId)
        {
            if (string.IsNullOrEmpty(panelId)) return false;
            return Preferences.ShowHud && WindowLayout.IsVisible(panelId);
        }
    }
}
