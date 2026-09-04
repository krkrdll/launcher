namespace Launcher.Settings
{
    /// <summary>
    /// A configurable modifier-key combination used for a global hotkey or in-app shortcut.
    /// </summary>
    public sealed class HotKeyDefinition
    {
        public bool ModifierCtrl { get; set; }
        public bool ModifierShift { get; set; }
        public bool ModifierAlt { get; set; }
        public bool ModifierWin { get; set; }
        public char Key { get; set; } = 'A';

        public bool HasModifier => ModifierCtrl || ModifierShift || ModifierAlt || ModifierWin;
    }
}
