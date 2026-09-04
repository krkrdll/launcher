using System;
using System.Collections.Generic;
using System.Drawing.Text;
using System.Linq;

namespace Launcher.Settings
{
    /// <summary>
    /// Enumerates the font families currently installed on the system.
    /// </summary>
    internal static class InstalledFontProvider
    {
        public static IReadOnlyList<string> GetFontFamilyNames()
        {
            try
            {
                using var installedFonts = new InstalledFontCollection();
                return installedFonts.Families
                    .Select(family => family.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to enumerate installed fonts: {ex.Message}");
                return Array.Empty<string>();
            }
        }
    }
}
