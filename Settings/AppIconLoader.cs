using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace Launcher.Settings
{
    /// <summary>
    /// Loads the shell icon for an executable or shortcut via the thumbnail cache.
    /// </summary>
    internal static class AppIconLoader
    {
        public static async Task<BitmapImage?> LoadIconAsync(string path)
        {
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(path);
                using var thumbnail = await file.GetThumbnailAsync(ThumbnailMode.SingleItem, 48);
                if (thumbnail is null)
                {
                    return null;
                }

                var bitmapImage = new BitmapImage();
                await bitmapImage.SetSourceAsync(thumbnail);
                return bitmapImage;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load icon for '{path}': {ex.Message}");
                return null;
            }
        }
    }
}
