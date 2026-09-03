using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Launcher.Settings;
using System;
using System.Collections.ObjectModel;
using Windows.Graphics;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Launcher
{
    /// <summary>
    /// Lets the user configure the global hotkey and startup behavior.
    /// </summary>
    public sealed partial class SettingsWindow : Window
    {
        private const int DefaultWidth = 420;
        private const int DefaultHeight = 620;

        private readonly AppWindow _appWindow;
        private readonly ObservableCollection<LaunchItem> _launchApps = new();

        public event Action? SettingsSaved;

        public SettingsWindow()
        {
            InitializeComponent();

            var hWnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            var settings = SettingsService.Load();
            ApplyWindowGeometry(settings);

            AppsListView.ItemsSource = _launchApps;
            PopulateKeyOptions(KeyComboBox);
            PopulateKeyOptions(ExpandKeyComboBox);
            LoadSettings(settings);

            Closed += (_, _) => SaveWindowGeometry();
        }

        private void ApplyWindowGeometry(AppSettings settings)
        {
            var width = settings.SettingsWindowWidth ?? DefaultWidth;
            var height = settings.SettingsWindowHeight ?? DefaultHeight;
            _appWindow.Resize(new SizeInt32(width, height));

            if (settings.SettingsWindowX is int x && settings.SettingsWindowY is int y)
            {
                _appWindow.Move(new PointInt32(x, y));
            }
        }

        private void SaveWindowGeometry()
        {
            var settings = SettingsService.Load();
            settings.SettingsWindowX = _appWindow.Position.X;
            settings.SettingsWindowY = _appWindow.Position.Y;
            settings.SettingsWindowWidth = _appWindow.Size.Width;
            settings.SettingsWindowHeight = _appWindow.Size.Height;
            SettingsService.Save(settings);
        }

        private static void PopulateKeyOptions(ComboBox comboBox)
        {
            for (var key = 'A'; key <= 'Z'; key++)
            {
                comboBox.Items.Add(key.ToString());
            }
        }

        private void LoadSettings(AppSettings settings)
        {
            CtrlCheckBox.IsChecked = settings.HotKeyModifierCtrl;
            ShiftCheckBox.IsChecked = settings.HotKeyModifierShift;
            AltCheckBox.IsChecked = settings.HotKeyModifierAlt;
            WinCheckBox.IsChecked = settings.HotKeyModifierWin;
            KeyComboBox.SelectedItem = settings.HotKeyKey.ToString();
            StartupCheckBox.IsChecked = StartupManager.IsEnabled();

            ExpandCtrlCheckBox.IsChecked = settings.ExpandModifierCtrl;
            ExpandShiftCheckBox.IsChecked = settings.ExpandModifierShift;
            ExpandAltCheckBox.IsChecked = settings.ExpandModifierAlt;
            ExpandWinCheckBox.IsChecked = settings.ExpandModifierWin;
            ExpandKeyComboBox.SelectedItem = settings.ExpandKey.ToString();

            _launchApps.Clear();
            foreach (var item in settings.LaunchItems)
            {
                _launchApps.Add(item);
            }
        }

        private async void AddAppButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            var hWnd = WindowNative.GetWindowHandle(this);
            InitializeWithWindow.Initialize(picker, hWnd);

            picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
            picker.FileTypeFilter.Add(".exe");
            picker.FileTypeFilter.Add(".lnk");

            var file = await picker.PickSingleFileAsync();
            if (file is null)
            {
                return;
            }

            var name = System.IO.Path.GetFileNameWithoutExtension(file.Name);
            _launchApps.Add(new LaunchItem { Name = name, Path = file.Path });
        }

        private void RemoveAppButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Microsoft.UI.Xaml.Controls.Button { Tag: LaunchItem item })
            {
                _launchApps.Remove(item);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var hasModifier = (CtrlCheckBox.IsChecked ?? false)
                || (ShiftCheckBox.IsChecked ?? false)
                || (AltCheckBox.IsChecked ?? false)
                || (WinCheckBox.IsChecked ?? false);

            if (!hasModifier || KeyComboBox.SelectedItem is not string keyText)
            {
                ValidationMessage.Visibility = Visibility.Visible;
                return;
            }

            var expandHasModifier = (ExpandCtrlCheckBox.IsChecked ?? false)
                || (ExpandShiftCheckBox.IsChecked ?? false)
                || (ExpandAltCheckBox.IsChecked ?? false)
                || (ExpandWinCheckBox.IsChecked ?? false);

            if (!expandHasModifier || ExpandKeyComboBox.SelectedItem is not string expandKeyText)
            {
                ExpandValidationMessage.Visibility = Visibility.Visible;
                return;
            }

            var settings = SettingsService.Load();
            settings.HotKeyModifierCtrl = CtrlCheckBox.IsChecked ?? false;
            settings.HotKeyModifierShift = ShiftCheckBox.IsChecked ?? false;
            settings.HotKeyModifierAlt = AltCheckBox.IsChecked ?? false;
            settings.HotKeyModifierWin = WinCheckBox.IsChecked ?? false;
            settings.HotKeyKey = keyText[0];
            settings.StartWithWindows = StartupCheckBox.IsChecked ?? false;

            settings.ExpandModifierCtrl = ExpandCtrlCheckBox.IsChecked ?? false;
            settings.ExpandModifierShift = ExpandShiftCheckBox.IsChecked ?? false;
            settings.ExpandModifierAlt = ExpandAltCheckBox.IsChecked ?? false;
            settings.ExpandModifierWin = ExpandWinCheckBox.IsChecked ?? false;
            settings.ExpandKey = expandKeyText[0];

            settings.LaunchItems = new System.Collections.Generic.List<LaunchItem>(_launchApps);

            SettingsService.Save(settings);
            StartupManager.SetEnabled(settings.StartWithWindows);

            SettingsSaved?.Invoke();
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
