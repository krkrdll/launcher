using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Launcher.Interop;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Graphics;
using WinRT.Interop;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Launcher
{
    /// <summary>
    /// Resides in the system tray; Win+Alt+H shows the window at the current mouse position.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private const int HotKeyId = 1;
        private const uint VirtualKeyH = 0x48;
        private const uint WM_TRAYICON = NativeMethods.WM_APP + 1;
        private const uint TrayIconId = 1;
        private const int MenuIdShow = 1;
        private const int MenuIdExit = 2;

        private readonly IntPtr _hWnd;
        private readonly AppWindow _appWindow;
        private readonly NativeMethods.WndProcDelegate _wndProcDelegate;
        private IntPtr _previousWndProc;
        private NativeMethods.NOTIFYICONDATA _trayIconData;
        private IntPtr _trayIconHandle;

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

        private void RegisterGlobalHotKey()
        {
            var registered = NativeMethods.RegisterHotKey(
                _hWnd,
                HotKeyId,
                NativeMethods.MOD_WIN | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT,
                VirtualKeyH);

            if (!registered)
            {
                Debug.WriteLine("Failed to register global hotkey Win+Alt+H. It may already be in use.");
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
                        DispatcherQueue.TryEnqueue(ShowAtCursorPosition);
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
