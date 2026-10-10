using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell;

namespace MehrBar.Spikes.Icons;

/// <summary>COM apartment of the extraction worker thread.</summary>
internal enum Apartment
{
    Mta,
    Sta,
}

/// <summary>
/// Outcome of one extraction. <see cref="Bgra"/> is the straight-alpha top-down 32 bpp image exactly as
/// read from the shell bitmap (not yet converted); <see cref="Ms"/> is the time spent inside the worker
/// (COM init included), or the wall time up to the hard timeout when the worker was abandoned.
/// <see cref="Error"/> holds only a short code, never a message that could carry a path.
/// </summary>
internal sealed record IconResult(int Width, int Height, byte[]? Bgra, double Ms, string? Error, string? BitsNote = null)
{
    public bool Ok => Bgra != null && Width > 0 && Height > 0;
}

/// <summary>
/// Shell icon extraction through <c>IShellItemImageFactory</c> with <c>SIIGBF_ICONONLY | SIIGBF_BIGGERSIZEOK</c>
/// (STACK section 1: the default flags return thumbnails / 44 px images). Pixels are read with
/// GetObject + GetDIBits as a top-down 32 bpp DIB so the straight alpha of the shell bitmap is preserved.
/// </summary>
internal static unsafe class ShellIcon
{
    private sealed class Box
    {
        public volatile IconResult? Result;
    }

    /// <summary>
    /// Extracts the icon of <paramref name="parsingName"/> on a fresh worker thread of the requested apartment.
    /// A hard <paramref name="timeout"/> abandons the worker (background thread, never joined again) and
    /// returns <c>Error = "timeout"</c>, so a hung path cannot block the caller.
    /// </summary>
    public static IconResult Get(string parsingName, int size, TimeSpan timeout, Apartment apartment = Apartment.Mta)
    {
        var box = new Box();
        var thread = new Thread(() => Work(parsingName, size, apartment, box))
        {
            IsBackground = true,
            Name = "icon-worker-" + apartment,
        };
        thread.SetApartmentState(apartment == Apartment.Sta ? ApartmentState.STA : ApartmentState.MTA);

        var wall = Stopwatch.StartNew();
        thread.Start();
        if (!thread.Join(timeout))
        {
            return new IconResult(0, 0, null, wall.Elapsed.TotalMilliseconds, "timeout");
        }

        return box.Result ?? new IconResult(0, 0, null, wall.Elapsed.TotalMilliseconds, "no-result");
    }

    private static string Hex(HRESULT hr) => "0x" + ((uint)hr.Value).ToString("X8");

    private static void Work(string parsingName, int size, Apartment apartment, Box box)
    {
        var sw = Stopwatch.StartNew();
        bool uninitialize = false;
        IShellItemImageFactory* factory = null;
        HBITMAP hbm = default;
        try
        {
            HRESULT co = PInvoke.CoInitializeEx(
                null,
                apartment == Apartment.Sta ? COINIT.COINIT_APARTMENTTHREADED : COINIT.COINIT_MULTITHREADED);
            // S_OK and S_FALSE both need a matching CoUninitialize; RPC_E_CHANGED_MODE does not.
            uninitialize = co.Succeeded;

            HRESULT hr;
            void* raw = null;
            Guid iid = IShellItemImageFactory.IID_Guid;
            fixed (char* path = parsingName)
            {
                hr = PInvoke.SHCreateItemFromParsingName(path, null, &iid, &raw);
            }

            if (hr.Failed || raw == null)
            {
                box.Result = new IconResult(0, 0, null, sw.Elapsed.TotalMilliseconds, "hr=" + Hex(hr));
                return;
            }

            factory = (IShellItemImageFactory*)raw;
            try
            {
                // CsWin32 projects GetImage as void and throws a COMException for a failed HRESULT.
                factory->GetImage(
                    new SIZE { cx = size, cy = size },
                    SIIGBF.SIIGBF_ICONONLY | SIIGBF.SIIGBF_BIGGERSIZEOK,
                    &hbm);
            }
            catch (COMException ce)
            {
                box.Result = new IconResult(0, 0, null, sw.Elapsed.TotalMilliseconds, "hr=0x" + ((uint)ce.HResult).ToString("X8"));
                return;
            }

            if (hbm.IsNull)
            {
                box.Result = new IconResult(0, 0, null, sw.Elapsed.TotalMilliseconds, "no-bitmap");
                return;
            }

            box.Result = ReadPixels(hbm, sw);
        }
        catch (Exception ex)
        {
            box.Result = new IconResult(0, 0, null, sw.Elapsed.TotalMilliseconds, "ex=" + ex.GetType().Name);
        }
        finally
        {
            if (!hbm.IsNull)
            {
                PInvoke.DeleteObject(hbm);
            }

            if (factory != null)
            {
                factory->Release();
            }

            if (uninitialize)
            {
                PInvoke.CoUninitialize();
            }
        }
    }

    private static IconResult ReadPixels(HBITMAP hbm, Stopwatch sw)
    {
        BITMAP bmp = default;
        if (PInvoke.GetObject(hbm, sizeof(BITMAP), &bmp) == 0)
        {
            return new IconResult(0, 0, null, sw.Elapsed.TotalMilliseconds, "getobject");
        }

        int w = bmp.bmWidth;
        int h = Math.Abs(bmp.bmHeight);
        if (w <= 0 || h <= 0)
        {
            return new IconResult(0, 0, null, sw.Elapsed.TotalMilliseconds, "empty-bitmap");
        }

        var info = new BITMAPINFO();
        info.bmiHeader.biSize = (uint)sizeof(BITMAPINFOHEADER);
        info.bmiHeader.biWidth = w;
        info.bmiHeader.biHeight = -h; // negative = top-down
        info.bmiHeader.biPlanes = 1;
        info.bmiHeader.biBitCount = 32;
        info.bmiHeader.biCompression = 0; // BI_RGB

        byte[] bgra = new byte[checked(w * h * 4)];
        HDC dc = PInvoke.GetDC(HWND.Null);
        int lines;
        try
        {
            fixed (byte* pixels = bgra)
            {
                lines = PInvoke.GetDIBits(dc, hbm, 0, (uint)h, pixels, &info, DIB_USAGE.DIB_RGB_COLORS);
            }
        }
        finally
        {
            PInvoke.ReleaseDC(HWND.Null, dc);
        }

        if (lines != h)
        {
            return new IconResult(0, 0, null, sw.Elapsed.TotalMilliseconds, "getdibits");
        }

        string? bitsNote = bmp.bmBitsPixel == 32 ? null : "bpp=" + bmp.bmBitsPixel;
        return new IconResult(w, h, bgra, sw.Elapsed.TotalMilliseconds, null, bitsNote);
    }
}
