using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Launcher.Interop;
using Launcher.Settings;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Graphics;
using WinRT.Interop;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Launcher
{
    /// <summary>
    /// Resides in the system tray; the configured global hotkey shows the window at the current mouse position.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private const int HotKeyId = 1;
        private const uint WM_TRAYICON = NativeMethods.WM_APP + 1;
        private const uint TrayIconId = 1;
        private const int MenuIdShow = 1;
        private const int MenuIdSettings = 2;
        private const int MenuIdExit = 3;

        private readonly IntPtr _hWnd;
        private readonly AppWindow _appWindow;
        private readonly NativeMethods.WndProcDelegate _wndProcDelegate;
        private IntPtr _previousWndProc;
        private NativeMethods.NOTIFYICONDATA _trayIconData;
        private IntPtr _trayIconHandle;
        private SettingsWindow? _settingsWindow;
        private readonly ObservableCollection<LaunchItemView> _launchItems = new();
        private readonly Dictionary<string, BitmapImage> _iconCache = new();

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
            InitializeTrayIcon();

            AppsGridView.ItemsSource = _launchItems;
            LoadLaunchItems();

            Activated += OnActivated;
        }

        private void ConfigureWindowChrome()
        {
            _appWindow.Resize(new SizeInt32(400, 200));

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

        private void ToggleAtCursorPosition()
        {
            if (_appWindow.IsVisible)
            {
                _appWindow.Hide();
            }
            else
            {
                ShowAtCursorPosition();
            }
        }

        private void ShowAtCursorPosition()
        {
            if (!NativeMethods.GetCursorPos(out var cursor))
            {
                return;
            }

            var point = new PointInt32(cursor.X, cursor.Y);
            var displayArea = DisplayArea.GetFromPoint(point, DisplayAreaFallback.Nearest);
            var workArea = displayArea.WorkArea;
            var size = _appWindow.Size;

            var x = ClampPosition(cursor.X, workArea.X, workArea.X + workArea.Width - size.Width);
            var y = ClampPosition(cursor.Y, workArea.Y, workArea.Y + workArea.Height - size.Height);

            _appWindow.Move(new PointInt32(x, y));
            _appWindow.Show();
            NativeMethods.SetForegroundWindow(_hWnd);
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
                        ShowAtCursorPosition();
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
                RegisterGlobalHotKey(SettingsService.Load());
                LoadLaunchItems();
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
                        DispatcherQueue.TryEnqueue(ToggleAtCursorPosition);
                        return IntPtr.Zero;
                    }
                    break;

                case var m when m == WM_TRAYICON:
                    var mouseMessage = (uint)lParam.ToInt32();
                    if (mouseMessage == NativeMethods.WM_LBUTTONUP)
                    {
                        DispatcherQueue.TryEnqueue(ShowAtCursorPosition);
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
