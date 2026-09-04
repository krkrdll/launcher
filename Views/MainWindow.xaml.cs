using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Launcher.Interop;
using Launcher.Settings;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Windows.Graphics;
using Windows.System;
using WinRT.Interop;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Launcher
{
    /// <summary>
    /// Resides in the system tray; the configured global hotkey shows the window at the current mouse position
    /// or a fixed screen position, depending on the configured display mode.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private const int HotKeyId = 1;
        private const uint WM_TRAYICON = NativeMethods.WM_APP + 1;
        private const uint TrayIconId = 1;
        private const int MenuIdShow = 1;
        private const int MenuIdSettings = 2;
        private const int MenuIdExit = 3;

        internal const int WindowWidth = 600;
        internal const int DefaultCollapsedHeight = 64;
        private const int ExpandedHeight = 230;
        private const int SuggestionsHeight = 200;

        private readonly IntPtr _hWnd;
        private readonly AppWindow _appWindow;
        private readonly NativeMethods.WndProcDelegate _wndProcDelegate;
        private IntPtr _previousWndProc;
        private NativeMethods.NOTIFYICONDATA _trayIconData;
        private IntPtr _trayIconHandle;
        private SettingsWindow? _settingsWindow;
        private readonly ObservableCollection<LaunchItemView> _launchItems = new();
        private readonly Dictionary<string, BitmapImage> _iconCache = new();
        private KeyboardAccelerator? _expandAccelerator;
        private bool _isExpanded;
        private bool _isShowingSuggestions;
        private int _collapsedHeight = DefaultCollapsedHeight;

        public MainWindow()
        {
            InitializeComponent();

            _hWnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(_hWnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            _wndProcDelegate = WndProc;
            _previousWndProc = NativeMethods.SetWindowProc(_hWnd, _wndProcDelegate);

            ConfigureWindowChrome();
            RegisterGlobalHotKey();
            RegisterExpandShortcut();
            RegisterSettingsShortcut();
            InitializeTrayIcon();

            AppsGridView.ItemsSource = _launchItems;
            LoadLaunchItems();
            ApplyLaunchTextBoxFont();
            LaunchTextBox.Loaded += (_, _) => UpdateCollapsedHeight();
            UpdateCollapsedHeight();

            Activated += OnActivated;
        }

        private void ConfigureWindowChrome()
        {
            _appWindow.Resize(new SizeInt32(WindowWidth, _collapsedHeight));

            if (_appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(true, false);
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
            }
        }

        private void OnActivated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated)
            {
                _appWindow.Hide();
            }
        }

        /// <summary>
        /// Hides the window so only the tray icon remains. Call once, right after the first Activate.
        /// </summary>
        public void HideToTray() => _appWindow.Hide();

        private void ApplyLaunchTextBoxFont() => ApplyLaunchTextBoxFont(SettingsService.Load());

        private void ApplyLaunchTextBoxFont(AppSettings settings)
        {
            LaunchTextBox.FontFamily = new FontFamily(settings.LaunchTextBoxFontFamily);
            LaunchTextBox.FontSize = settings.LaunchTextBoxFontSize;
        }

        /// <summary>
        /// Recomputes the collapsed window height from the text box's actual desired size,
        /// so a larger font doesn't get clipped by a fixed window height.
        /// </summary>
        private void UpdateCollapsedHeight()
        {
            LaunchTextBox.Measure(new Windows.Foundation.Size(WindowWidth, double.PositiveInfinity));
            var desiredHeight = LaunchTextBox.DesiredSize.Height;
            if (desiredHeight <= 0)
            {
                return;
            }

            var verticalMargin = LaunchTextBox.Margin.Top + LaunchTextBox.Margin.Bottom;
            _collapsedHeight = Math.Max(DefaultCollapsedHeight, (int)Math.Ceiling(desiredHeight + verticalMargin));
            ResizeToCurrentState();
        }

        private void RegisterGlobalHotKey() => RegisterGlobalHotKey(SettingsService.Load());

        private void RegisterGlobalHotKey(AppSettings settings)
        {
            NativeMethods.UnregisterHotKey(_hWnd, HotKeyId);

            var modifiers = NativeMethods.MOD_NOREPEAT;
            if (settings.HotKeyModifierCtrl) modifiers |= NativeMethods.MOD_CONTROL;
            if (settings.HotKeyModifierShift) modifiers |= NativeMethods.MOD_SHIFT;
            if (settings.HotKeyModifierAlt) modifiers |= NativeMethods.MOD_ALT;
            if (settings.HotKeyModifierWin) modifiers |= NativeMethods.MOD_WIN;
            var virtualKey = (uint)char.ToUpperInvariant(settings.HotKeyKey);

            var registered = NativeMethods.RegisterHotKey(_hWnd, HotKeyId, modifiers, virtualKey);
            if (!registered)
            {
                Debug.WriteLine("Failed to register global hotkey. It may already be in use.");
            }
        }

        private void RegisterExpandShortcut() => RegisterExpandShortcut(SettingsService.Load());

        private void RegisterExpandShortcut(AppSettings settings)
        {
            if (_expandAccelerator is not null)
            {
                RootGrid.KeyboardAccelerators.Remove(_expandAccelerator);
            }

            if (!Enum.TryParse<VirtualKey>(settings.ExpandKey.ToString(), out var virtualKey))
            {
                return;
            }

            var modifiers = VirtualKeyModifiers.None;
            if (settings.ExpandModifierCtrl) modifiers |= VirtualKeyModifiers.Control;
            if (settings.ExpandModifierShift) modifiers |= VirtualKeyModifiers.Shift;
            if (settings.ExpandModifierAlt) modifiers |= VirtualKeyModifiers.Menu;
            if (settings.ExpandModifierWin) modifiers |= VirtualKeyModifiers.Windows;

            _expandAccelerator = new KeyboardAccelerator { Key = virtualKey, Modifiers = modifiers };
            _expandAccelerator.Invoked += ExpandAccelerator_Invoked;
            RootGrid.KeyboardAccelerators.Add(_expandAccelerator);
        }

        private void ExpandAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            SetExpanded(!_isExpanded);
        }

        private void RegisterSettingsShortcut()
        {
            var accelerator = new KeyboardAccelerator { Key = VirtualKey.O, Modifiers = VirtualKeyModifiers.Control };
            accelerator.Invoked += SettingsAccelerator_Invoked;
            RootGrid.KeyboardAccelerators.Add(accelerator);
        }

        private void SettingsAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            OpenSettingsWindow();
        }

        private void SetExpanded(bool expanded)
        {
            if (_isExpanded == expanded)
            {
                return;
            }

            _isExpanded = expanded;
            UpdateContentVisibility();
            ResizeToCurrentState();
        }

        private void UpdateContentVisibility()
        {
            ContentGrid.Visibility = _isExpanded && !_isShowingSuggestions ? Visibility.Visible : Visibility.Collapsed;
        }

        private int CurrentContentHeight()
        {
            if (_isShowingSuggestions)
            {
                return _collapsedHeight + SuggestionsHeight;
            }

            return _isExpanded ? ExpandedHeight + (_collapsedHeight - DefaultCollapsedHeight) : _collapsedHeight;
        }

        private void ResizeToCurrentState()
        {
            var size = new SizeInt32(WindowWidth, CurrentContentHeight());
            var displayArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Nearest);
            var workArea = displayArea.WorkArea;
            var position = _appWindow.Position;

            _appWindow.Resize(size);

            var x = ClampPosition(position.X, workArea.X, workArea.X + workArea.Width - size.Width);
            var y = ClampPosition(position.Y, workArea.Y, workArea.Y + workArea.Height - size.Height);
            _appWindow.Move(new PointInt32(x, y));
        }

        private void InitializeTrayIcon()
        {
            _trayIconHandle = ExtractApplicationIcon();

            _trayIconData = new NativeMethods.NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf<NativeMethods.NOTIFYICONDATA>(),
                hWnd = _hWnd,
                uID = TrayIconId,
                uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP,
                uCallbackMessage = WM_TRAYICON,
                hIcon = _trayIconHandle,
                szTip = "Launcher",
                szInfo = string.Empty,
                szInfoTitle = string.Empty,
            };

            NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_ADD, ref _trayIconData);
        }

        private static IntPtr ExtractApplicationIcon()
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            var icon = string.IsNullOrEmpty(exePath) ? IntPtr.Zero : NativeMethods.ExtractIcon(IntPtr.Zero, exePath, 0);
            return icon != IntPtr.Zero ? icon : NativeMethods.LoadIcon(IntPtr.Zero, NativeMethods.IDI_APPLICATION);
        }

        private void ToggleLauncherWindow()
        {
            if (_appWindow.IsVisible)
            {
                _appWindow.Hide();
            }
            else
            {
                ShowLauncherWindow();
            }
        }

        private void ShowLauncherWindow()
        {
            var size = new SizeInt32(WindowWidth, _collapsedHeight);
            var position = ComputeShowPosition(SettingsService.Load(), size);
            if (position is null)
            {
                return;
            }

            _isExpanded = false;
            _isShowingSuggestions = false;
            ContentGrid.Visibility = Visibility.Collapsed;
            SuggestionsListView.Visibility = Visibility.Collapsed;
            SuggestionsListView.ItemsSource = null;

            _appWindow.Resize(size);
            _appWindow.Move(position.Value);
            _appWindow.Show();
            NativeMethods.SetForegroundWindow(_hWnd);

            LaunchTextBox.Text = string.Empty;
            LaunchTextBox.Focus(FocusState.Programmatic);
        }

        private static PointInt32? ComputeShowPosition(AppSettings settings, SizeInt32 size)
        {
            if (settings.LauncherPositionMode == LauncherPositionMode.Fixed)
            {
                var workArea = DisplayArea.Primary.WorkArea;
                var fixedX = ClampPosition(settings.LauncherFixedPositionX, workArea.X, workArea.X + workArea.Width - size.Width);
                var fixedY = ClampPosition(settings.LauncherFixedPositionY, workArea.Y, workArea.Y + workArea.Height - size.Height);
                return new PointInt32(fixedX, fixedY);
            }

            if (!NativeMethods.GetCursorPos(out var cursor))
            {
                return null;
            }

            var displayArea = DisplayArea.GetFromPoint(new PointInt32(cursor.X, cursor.Y), DisplayAreaFallback.Nearest);
            var cursorWorkArea = displayArea.WorkArea;

            var x = ClampPosition(cursor.X, cursorWorkArea.X, cursorWorkArea.X + cursorWorkArea.Width - size.Width);
            var y = ClampPosition(cursor.Y, cursorWorkArea.Y, cursorWorkArea.Y + cursorWorkArea.Height - size.Height);
            return new PointInt32(x, y);
        }

        private static int ClampPosition(int value, int min, int max) => max <= min ? min : Math.Clamp(value, min, max);

        private void LoadLaunchItems()
        {
            var settings = SettingsService.Load();
            _launchItems.Clear();

            foreach (var item in settings.LaunchItems)
            {
                var view = new LaunchItemView { Name = item.Name, Path = item.Path };
                if (_iconCache.TryGetValue(item.Path, out var cachedIcon))
                {
                    view.IconSource = cachedIcon;
                }
                else
                {
                    _ = LoadIconAsync(item.Path, view);
                }

                _launchItems.Add(view);
            }

            EmptyStateText.Visibility = _launchItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private async System.Threading.Tasks.Task LoadIconAsync(string path, LaunchItemView view)
        {
            var icon = await AppIconLoader.LoadIconAsync(path);
            if (icon is null)
            {
                return;
            }

            _iconCache[path] = icon;
            view.IconSource = icon;
        }

        private void AppsGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is LaunchItemView item)
            {
                LaunchApp(item.Path);
            }
        }

        private void LaunchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var text = LaunchTextBox.Text;
            if (string.IsNullOrEmpty(text))
            {
                SetSuggestions(null);
                return;
            }

            var suggestions = _launchItems
                .Where(item => item.Name.StartsWith(text, StringComparison.OrdinalIgnoreCase))
                .ToList();

            SetSuggestions(suggestions);
        }

        private void SetSuggestions(List<LaunchItemView>? suggestions)
        {
            var visible = suggestions is { Count: > 0 };

            SuggestionsListView.ItemsSource = suggestions;
            SuggestionsListView.SelectedIndex = -1;
            SuggestionsListView.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

            if (_isShowingSuggestions == visible)
            {
                return;
            }

            _isShowingSuggestions = visible;
            UpdateContentVisibility();
            ResizeToCurrentState();
        }

        private void LaunchTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            switch (e.Key)
            {
                case VirtualKey.Down when _isShowingSuggestions:
                    MoveSuggestionSelection(1);
                    e.Handled = true;
                    break;

                case VirtualKey.Up when _isShowingSuggestions:
                    MoveSuggestionSelection(-1);
                    e.Handled = true;
                    break;

                case VirtualKey.Enter:
                    LaunchHighlightedOrSingleSuggestion();
                    break;
            }
        }

        private void MoveSuggestionSelection(int delta)
        {
            var count = SuggestionsListView.Items.Count;
            if (count == 0)
            {
                return;
            }

            var next = Math.Clamp(SuggestionsListView.SelectedIndex + delta, -1, count - 1);
            SuggestionsListView.SelectedIndex = next;

            if (next >= 0)
            {
                SuggestionsListView.ScrollIntoView(SuggestionsListView.SelectedItem);
            }
        }

        private void LaunchHighlightedOrSingleSuggestion()
        {
            if (SuggestionsListView.SelectedItem is LaunchItemView selected)
            {
                LaunchSelection(selected);
                return;
            }

            if (SuggestionsListView.ItemsSource is IReadOnlyCollection<LaunchItemView> { Count: 1 } suggestions)
            {
                LaunchSelection(suggestions.Single());
            }
        }

        private void SuggestionsListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is LaunchItemView item)
            {
                LaunchSelection(item);
            }
        }

        private void LaunchSelection(LaunchItemView item)
        {
            LaunchTextBox.Text = string.Empty;
            LaunchApp(item.Path);
        }

        private void LaunchApp(string path)
        {
            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to launch '{path}': {ex.Message}");
            }

            _appWindow.Hide();
        }

        private void ShowTrayContextMenu()
        {
            var hMenu = NativeMethods.CreatePopupMenu();
            if (hMenu == IntPtr.Zero)
            {
                return;
            }

            try
            {
                NativeMethods.AppendMenu(hMenu, NativeMethods.MF_STRING, (UIntPtr)MenuIdShow, "表示");
                NativeMethods.AppendMenu(hMenu, NativeMethods.MF_STRING, (UIntPtr)MenuIdSettings, "設定");
                NativeMethods.AppendMenu(hMenu, NativeMethods.MF_STRING, (UIntPtr)MenuIdExit, "終了");

                NativeMethods.GetCursorPos(out var cursor);
                NativeMethods.SetForegroundWindow(_hWnd);
                var command = NativeMethods.TrackPopupMenuEx(
                    hMenu,
                    NativeMethods.TPM_RIGHTBUTTON | NativeMethods.TPM_RETURNCMD,
                    cursor.X,
                    cursor.Y,
                    _hWnd,
                    IntPtr.Zero);
                NativeMethods.PostMessage(_hWnd, NativeMethods.WM_NULL, IntPtr.Zero, IntPtr.Zero);

                switch (command)
                {
                    case MenuIdShow:
                        ShowLauncherWindow();
                        break;
                    case MenuIdSettings:
                        OpenSettingsWindow();
                        break;
                    case MenuIdExit:
                        ExitApplication();
                        break;
                }
            }
            finally
            {
                NativeMethods.DestroyMenu(hMenu);
            }
        }

        private void OpenSettingsWindow()
        {
            if (_settingsWindow is not null)
            {
                _settingsWindow.Activate();
                return;
            }

            _settingsWindow = new SettingsWindow();
            _settingsWindow.SettingsSaved += () =>
            {
                var settings = SettingsService.Load();
                RegisterGlobalHotKey(settings);
                RegisterExpandShortcut(settings);
                LoadLaunchItems();
                ApplyLaunchTextBoxFont(settings);
                UpdateCollapsedHeight();
            };
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Activate();
        }

        private void ExitApplication()
        {
            NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, ref _trayIconData);
            if (_trayIconHandle != IntPtr.Zero)
            {
                NativeMethods.DestroyIcon(_trayIconHandle);
                _trayIconHandle = IntPtr.Zero;
            }

            NativeMethods.UnregisterHotKey(_hWnd, HotKeyId);

            if (_previousWndProc != IntPtr.Zero)
            {
                NativeMethods.SetWindowProc(_hWnd, _previousWndProc);
                _previousWndProc = IntPtr.Zero;
            }

            Application.Current.Exit();
        }

        private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            switch (msg)
            {
                case NativeMethods.WM_HOTKEY:
                    if (wParam.ToInt32() == HotKeyId)
                    {
                        DispatcherQueue.TryEnqueue(ToggleLauncherWindow);
                        return IntPtr.Zero;
                    }
                    break;

                case var m when m == WM_TRAYICON:
                    var mouseMessage = (uint)lParam.ToInt32();
                    if (mouseMessage == NativeMethods.WM_LBUTTONUP)
                    {
                        DispatcherQueue.TryEnqueue(ShowLauncherWindow);
                    }
                    else if (mouseMessage == NativeMethods.WM_RBUTTONUP || mouseMessage == NativeMethods.WM_CONTEXTMENU)
                    {
                        DispatcherQueue.TryEnqueue(ShowTrayContextMenu);
                    }
                    return IntPtr.Zero;

                case NativeMethods.WM_CLOSE:
                    DispatcherQueue.TryEnqueue(HideToTray);
                    return IntPtr.Zero;
            }

            return NativeMethods.CallWindowProc(_previousWndProc, hWnd, msg, wParam, lParam);
        }
    }
}
