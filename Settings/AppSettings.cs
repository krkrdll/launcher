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
        public HotKeyDefinition ToggleWindowHotKey { get; set; } = new() { ModifierAlt = true, ModifierWin = true, Key = 'H' };
        public bool StartWithWindows { get; set; }

        public HotKeyDefinition ExpandIconsHotKey { get; set; } = new() { ModifierCtrl = true, Key = 'I' };

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
