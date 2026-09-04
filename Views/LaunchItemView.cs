using Microsoft.UI.Xaml.Media.Imaging;
using System.ComponentModel;

namespace Launcher
{
    /// <summary>
    /// Display state for one registered app shown as an icon in the launcher window.
    /// </summary>
    internal sealed class LaunchItemView : INotifyPropertyChanged
    {
        private BitmapImage? _iconSource;

        public required string Name { get; init; }
        public required string Path { get; init; }
        public string Arguments { get; init; } = string.Empty;

        public BitmapImage? IconSource
        {
            get => _iconSource;
            set
            {
                if (_iconSource != value)
                {
                    _iconSource = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IconSource)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
