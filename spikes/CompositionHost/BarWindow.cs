using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MehrBar.Spikes.CompositionHost;

/// <summary>
/// The bar window for DirectComposition: a popup without a redirection bitmap (WS_EX_NOREDIRECTIONBITMAP), so the DWM
/// shows only the composition visual and no GDI surface; tool window, never activated. Same rectangle as the WindowHost
/// bar (top centre of the primary monitor, 1900x250) and, like it, not topmost.
/// </summary>
internal sealed unsafe class BarWindow : IDisposable
{
    private const string ClassName = "MehrBarCompositionHost";

    private static bool s_closed;

    public BarWindow(int width, int height)
    {
        HINSTANCE instance = (HINSTANCE)(nint)PInvoke.GetModuleHandle((char*)null).Value;
        fixed (char* className = ClassName)
        {
            var wc = new WNDCLASSEXW
            {
                cbSize = (uint)sizeof(WNDCLASSEXW),
                lpfnWndProc = &WndProc,
                hInstance = instance,
                lpszClassName = className,
            };
            if (PInvoke.RegisterClassEx(&wc) == 0)
            {
                throw new InvalidOperationException($"RegisterClassEx failed: {Marshal.GetLastWin32Error()}");
            }

            (int x, int y) = Common.SpikeGeometry.DefaultWindowPos(width);
            Hwnd = PInvoke.CreateWindowEx(
                WINDOW_EX_STYLE.WS_EX_NOREDIRECTIONBITMAP | WINDOW_EX_STYLE.WS_EX_TOOLWINDOW | WINDOW_EX_STYLE.WS_EX_NOACTIVATE,
                className,
                className,
                WINDOW_STYLE.WS_POPUP | WINDOW_STYLE.WS_VISIBLE,
                x,
                y,
                width,
                height,
                HWND.Null,
                HMENU.Null,
                instance,
                null);
        }

        if (Hwnd.IsNull)
        {
            throw new InvalidOperationException($"CreateWindowEx failed: {Marshal.GetLastWin32Error()}");
        }
    }

    public HWND Hwnd { get; }

    public bool Closed => s_closed;

    /// <summary>Dispatches all pending messages.</summary>
    public void Pump()
    {
        MSG msg;
        while (PInvoke.PeekMessage(&msg, HWND.Null, 0, 0, PEEK_MESSAGE_REMOVE_TYPE.PM_REMOVE))
        {
            PInvoke.TranslateMessage(&msg);
            PInvoke.DispatchMessage(&msg);
        }
    }

    /// <summary>Sleeps until a message arrives or <paramref name="timeoutMs"/> passes, then pumps.</summary>
    public void WaitForMessages(uint timeoutMs)
    {
        PInvoke.MsgWaitForMultipleObjects(0, null, false, timeoutMs, QUEUE_STATUS_FLAGS.QS_ALLINPUT);
        Pump();
    }

    public void Dispose()
    {
        if (!s_closed)
        {
            PInvoke.DestroyWindow(Hwnd);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    private static LRESULT WndProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        switch (msg)
        {
            case PInvoke.WM_MOUSEACTIVATE:
                return (LRESULT)(nint)PInvoke.MA_NOACTIVATE;
            case PInvoke.WM_DESTROY:
                s_closed = true;
                PInvoke.PostQuitMessage(0);
                return (LRESULT)0;
            default:
                return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
        }
    }
}
