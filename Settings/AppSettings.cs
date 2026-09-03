using System.Collections.Generic;

namespace Launcher.Settings
{
    public enum LauncherPositionMode
    {
        Cursor,
        Fixed,
    }

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

        public bool ExpandModifierCtrl { get; set; } = true;
        public bool ExpandModifierShift { get; set; }
        public bool ExpandModifierAlt { get; set; }
        public bool ExpandModifierWin { get; set; }
        public char ExpandKey { get; set; } = 'I';

        public int? SettingsWindowX { get; set; }
        public int? SettingsWindowY { get; set; }
        public int? SettingsWindowWidth { get; set; }
        public int? SettingsWindowHeight { get; set; }

        public LauncherPositionMode LauncherPositionMode { get; set; } = LauncherPositionMode.Cursor;
        public int LauncherFixedPositionX { get; set; } = 100;
        public int LauncherFixedPositionY { get; set; } = 100;

        public string LaunchTextBoxFontFamily { get; set; } = "Segoe UI Variable";
        public double LaunchTextBoxFontSize { get; set; } = 14;

        public List<LaunchItem> LaunchItems { get; set; } = new();
    }
}
