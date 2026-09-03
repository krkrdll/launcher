using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Launcher.Interop;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Graphics;
using WinRT.Interop;

namespace Launcher
{
    /// <summary>
    /// Borderless, always-on-top preview that follows the mouse cursor anywhere on screen.
    /// A left click reports the (clamped) top-left position where the launcher window would
    /// be placed; a right click cancels. Tracking uses a low-level mouse hook so it works even
    /// while this window itself doesn't have focus.
    /// </summary>
    public sealed partial class PositionPickerWindow : Window
    {
        private readonly IntPtr _hWnd;
        private readonly NativeMethods.LowLevelMouseProc _hookProc;
        private IntPtr _hookHandle;
        private bool _resolved;

        public event Action<PointInt32>? PositionPicked;
        public event Action? Cancelled;

        public PositionPickerWindow()
        {
            InitializeComponent();

            _hWnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(_hWnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(false, false);
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
            }

            appWindow.Resize(new SizeInt32(MainWindow.WindowWidth, MainWindow.CollapsedHeight));

            _hookProc = HookProc;
            _hookHandle = NativeMethods.SetWindowsHookEx(
                NativeMethods.WH_MOUSE_LL,
                _hookProc,
                NativeMethods.GetModuleHandle(null),
                0);

            if (_hookHandle == IntPtr.Zero)
            {
                Debug.WriteLine("Failed to install the low-level mouse hook for position picking.");
            }

            MoveToCursor();
            ShowTopmostWithoutActivating();

            Closed += (_, _) => RemoveHook();
        }

        private void ShowTopmostWithoutActivating()
        {
            NativeMethods.SetWindowPos(
                _hWnd,
                NativeMethods.HWND_TOPMOST,
                0, 0, 0, 0,
                NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
        }

        private void MoveWindow(int x, int y)
        {
            NativeMethods.SetWindowPos(
                _hWnd,
                NativeMethods.HWND_TOPMOST,
                x, y, 0, 0,
                NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
        }

        private void MoveToCursor()
        {
            if (!NativeMethods.GetCursorPos(out var cursor))
            {
                return;
            }

            var (x, y) = ClampedTopLeft(cursor.X, cursor.Y);
            MoveWindow(x, y);
        }

        private static (int X, int Y) ClampedTopLeft(int cursorX, int cursorY)
        {
            var displayArea = DisplayArea.GetFromPoint(new PointInt32(cursorX, cursorY), DisplayAreaFallback.Nearest);
            var workArea = displayArea.WorkArea;

            var x = Math.Clamp(cursorX, workArea.X, Math.Max(workArea.X, workArea.X + workArea.Width - MainWindow.WindowWidth));
            var y = Math.Clamp(cursorY, workArea.Y, Math.Max(workArea.Y, workArea.Y + workArea.Height - MainWindow.CollapsedHeight));
            return (x, y);
        }

        private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && !_resolved)
            {
                var message = (uint)wParam.ToInt32();

                if (message == NativeMethods.WM_MOUSEMOVE)
                {
                    var data = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                    var (x, y) = ClampedTopLeft(data.pt.X, data.pt.Y);
                    DispatcherQueue.TryEnqueue(() => MoveWindow(x, y));
                }
                else if (message == NativeMethods.WM_LBUTTONDOWN)
                {
                    var data = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                    var (x, y) = ClampedTopLeft(data.pt.X, data.pt.Y);
                    _resolved = true;
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        PositionPicked?.Invoke(new PointInt32(x, y));
                        Close();
                    });
                }
                else if (message == NativeMethods.WM_RBUTTONDOWN)
                {
                    _resolved = true;
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        Cancelled?.Invoke();
                        Close();
                    });
                }
            }

            return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        private void RemoveHook()
        {
            if (_hookHandle != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hookHandle);
                _hookHandle = IntPtr.Zero;
            }
        }
    }
}
