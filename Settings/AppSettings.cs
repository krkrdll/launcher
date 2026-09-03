using System.Collections.Generic;

namespace Launcher.Settings
{
    /// <summary>
    /// User-configurable application settings, persisted as JSON.
    /// </summary>
    public sealed class AppSettings
    {
        public bool HotKeyModifierCtrl { get; set; }
        public bool HotKeyModifierShift { get; set; }
        public bool HotKeyModifierAlt { get; set; } = true;
        public bool HotKeyModifierWin { get; set; } = true;
        public char HotKeyKey { get; set; } = 'H';
        public bool StartWithWindows { get; set; }

        public int? SettingsWindowX { get; set; }
        public int? SettingsWindowY { get; set; }
        public int? SettingsWindowWidth { get; set; }
        public int? SettingsWindowHeight { get; set; }

        public List<LaunchItem> LaunchItems { get; set; } = new();
    }
}
