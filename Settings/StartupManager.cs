using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace Launcher.Settings
{
    /// <summary>
    /// Registers or unregisters the app to launch at Windows sign-in via the current user's Run key.
    /// </summary>
    internal static class StartupManager
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "Launcher";

        public static bool IsEnabled()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is not null;
        }

        public static void SetEnabled(bool enabled)
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key is null)
            {
                return;
            }

            if (enabled)
            {
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(ValueName, $"\"{exePath}\"");
                }
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
    }
}
