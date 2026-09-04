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
        private const int DefaultWidth = 680;
        private const int DefaultHeight = 560;

        private readonly AppWindow _appWindow;
        private readonly ObservableCollection<LaunchItem> _launchApps = new();
        private PositionPickerWindow? _positionPickerWindow;

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
            PopulateFontFamilies();
            LoadSettings(settings);

            // IsEditable な ComboBox は、ItemsSource から候補一覧(ポップアップ)が生成されるより前に
            // Text を設定すると編集用テキストボックスの表示に反映されないことがある。
            // レイアウトが一巡した後まで再設定を遅延させることで確実に表示させる。
            DispatcherQueue.TryEnqueue(() => FontFamilyComboBox.Text = settings.LaunchTextBoxFontFamily);

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

        private void PopulateFontFamilies()
        {
            FontFamilyComboBox.ItemsSource = InstalledFontProvider.GetFontFamilyNames();
        }

        private void LoadSettings(AppSettings settings)
        {
            ToggleWindowHotKeyEditor.SetHotKey(settings.ToggleWindowHotKey);
            StartupCheckBox.IsChecked = StartupManager.IsEnabled();

            ExpandIconsHotKeyEditor.SetHotKey(settings.ExpandIconsHotKey);

            PositionCursorRadioButton.IsChecked = settings.LauncherPositionMode == LauncherPositionMode.Cursor;
            PositionFixedRadioButton.IsChecked = settings.LauncherPositionMode == LauncherPositionMode.Fixed;
            FixedPositionXNumberBox.Value = settings.LauncherFixedPositionX;
            FixedPositionYNumberBox.Value = settings.LauncherFixedPositionY;
            UpdateFixedPositionInputsEnabled();

            FontFamilyComboBox.Text = settings.LaunchTextBoxFontFamily;
            FontSizeNumberBox.Value = settings.LaunchTextBoxFontSize;

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

        private void PositionModeRadioButton_Checked(object sender, RoutedEventArgs e) => UpdateFixedPositionInputsEnabled();

        private void UpdateFixedPositionInputsEnabled()
        {
            var enabled = PositionFixedRadioButton.IsChecked ?? false;
            FixedPositionXNumberBox.IsEnabled = enabled;
            FixedPositionYNumberBox.IsEnabled = enabled;
            PickPositionButton.IsEnabled = enabled;
        }

        private void PickPositionButton_Click(object sender, RoutedEventArgs e)
        {
            if (_positionPickerWindow is not null)
            {
                return;
            }

            _positionPickerWindow = new PositionPickerWindow();
            _positionPickerWindow.PositionPicked += OnPositionPicked;
            _positionPickerWindow.Closed += (_, _) => _positionPickerWindow = null;
        }

        private void OnPositionPicked(PointInt32 position)
        {
            FixedPositionXNumberBox.Value = position.X;
            FixedPositionYNumberBox.Value = position.Y;
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
            if (!ToggleWindowHotKeyEditor.TryGetHotKey(out var toggleWindowHotKey))
            {
                return;
            }

            if (!ExpandIconsHotKeyEditor.TryGetHotKey(out var expandIconsHotKey))
            {
                return;
            }

            var settings = SettingsService.Load();
            settings.ToggleWindowHotKey = toggleWindowHotKey;
            settings.StartWithWindows = StartupCheckBox.IsChecked ?? false;

            settings.ExpandIconsHotKey = expandIconsHotKey;

            settings.LauncherPositionMode = (PositionFixedRadioButton.IsChecked ?? false)
                ? LauncherPositionMode.Fixed
                : LauncherPositionMode.Cursor;

            if (!double.IsNaN(FixedPositionXNumberBox.Value))
            {
                settings.LauncherFixedPositionX = (int)FixedPositionXNumberBox.Value;
            }

            if (!double.IsNaN(FixedPositionYNumberBox.Value))
            {
                settings.LauncherFixedPositionY = (int)FixedPositionYNumberBox.Value;
            }

            var fontFamily = FontFamilyComboBox.Text?.Trim();
            settings.LaunchTextBoxFontFamily = string.IsNullOrEmpty(fontFamily) ? new AppSettings().LaunchTextBoxFontFamily : fontFamily;
            settings.LaunchTextBoxFontSize = double.IsNaN(FontSizeNumberBox.Value) ? new AppSettings().LaunchTextBoxFontSize : FontSizeNumberBox.Value;

            settings.LaunchItems = new System.Collections.Generic.List<LaunchItem>(_launchApps);

            SettingsService.Save(settings);
            StartupManager.SetEnabled(settings.StartWithWindows);

            SettingsSaved?.Invoke();
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();

        private void SettingsNavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            var tag = (args.SelectedItemContainer as NavigationViewItem)?.Tag as string;

            ShortcutsPanel.Visibility = tag == "Shortcuts" ? Visibility.Visible : Visibility.Collapsed;
            DisplayPanel.Visibility = tag == "Display" ? Visibility.Visible : Visibility.Collapsed;
            StartupPanel.Visibility = tag == "Startup" ? Visibility.Visible : Visibility.Collapsed;
            AppsPanel.Visibility = tag == "Apps" ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
