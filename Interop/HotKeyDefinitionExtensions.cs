using Launcher.Settings;
using Windows.System;

namespace Launcher.Interop
{
    /// <summary>
    /// Converts a <see cref="HotKeyDefinition"/> into the modifier flag formats required by the
    /// Win32 hotkey APIs and by WinUI <see cref="Microsoft.UI.Xaml.Input.KeyboardAccelerator"/>.
    /// </summary>
    internal static class HotKeyDefinitionExtensions
    {
        public static uint ToWin32Modifiers(this HotKeyDefinition hotKey, bool noRepeat = false)
        {
            var modifiers = noRepeat ? NativeMethods.MOD_NOREPEAT : 0u;
            if (hotKey.ModifierCtrl) modifiers |= NativeMethods.MOD_CONTROL;
            if (hotKey.ModifierShift) modifiers |= NativeMethods.MOD_SHIFT;
            if (hotKey.ModifierAlt) modifiers |= NativeMethods.MOD_ALT;
            if (hotKey.ModifierWin) modifiers |= NativeMethods.MOD_WIN;
            return modifiers;
        }

        public static uint ToWin32VirtualKey(this HotKeyDefinition hotKey) => (uint)char.ToUpperInvariant(hotKey.Key);

        public static VirtualKeyModifiers ToVirtualKeyModifiers(this HotKeyDefinition hotKey)
        {
            var modifiers = VirtualKeyModifiers.None;
            if (hotKey.ModifierCtrl) modifiers |= VirtualKeyModifiers.Control;
            if (hotKey.ModifierShift) modifiers |= VirtualKeyModifiers.Shift;
            if (hotKey.ModifierAlt) modifiers |= VirtualKeyModifiers.Menu;
            if (hotKey.ModifierWin) modifiers |= VirtualKeyModifiers.Windows;
            return modifiers;
        }
    }
}
