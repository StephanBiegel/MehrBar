using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Direct2D;
using Windows.Win32.Graphics.Direct2D.Common;
using Windows.Win32.Graphics.Direct3D;
using Windows.Win32.Graphics.Direct3D11;
using Windows.Win32.Graphics.DirectComposition;
using Windows.Win32.Graphics.Dxgi;
using Windows.Win32.Graphics.Dxgi.Common;
using Windows.Win32.System.Com;

namespace MehrBar.Spikes.CompositionHost;

/// <summary>
/// D3D11 device, a composition swap chain (flip sequential, 2 buffers, premultiplied alpha, frame-latency waitable with
/// maximum latency 1), a DirectComposition target/visual on the window and a Direct2D device context drawing into the
/// back buffer. Icons are the synthetic 256 px discs of the WindowHost workload, uploaded once as D2D bitmaps.
/// </summary>
[SupportedOSPlatform("windows8.1")]
internal sealed unsafe class CompositionRenderer : IDisposable
{
    private readonly Options _options;
    private readonly int _width;
    private readonly int _height;
    private readonly ID3D11Device* _device;
    private readonly IDXGIDevice* _dxgiDevice;
    private readonly IDXGISwapChain2* _swapChain;
    private readonly HANDLE _frameWait;
    private readonly IDCompositionDevice* _dcomp;
    private readonly IDCompositionTarget* _target;
    private readonly IDCompositionVisual* _visual;
    private readonly ID2D1Factory1* _d2dFactory;
    private readonly ID2D1Device* _d2dDevice;
    private readonly ID2D1DeviceContext* _dc;
    private readonly ID2D1Bitmap1* _backBuffer;
    private readonly ID2D1Bitmap1*[] _icons;
    private readonly ID2D1SolidColorBrush* _black;

    public CompositionRenderer(HWND hwnd, Options options)
    {
        _options = options;
        _width = options.Width;
        _height = options.Height;

        ID3D11Device* device = null;
        PInvoke.D3D11CreateDevice(
            null, D3D_DRIVER_TYPE.D3D_DRIVER_TYPE_HARDWARE, HMODULE.Null, D3D11_CREATE_DEVICE_FLAG.D3D11_CREATE_DEVICE_BGRA_SUPPORT,
            null, 0, PInvoke.D3D11_SDK_VERSION, &device, null, null).ThrowOnFailure();
        _device = device;

        IDXGIDevice* dxgiDevice = null;
        Guid iidDxgiDevice = IDXGIDevice.IID_Guid;
        ((IUnknown*)device)->QueryInterface(&iidDxgiDevice, (void**)&dxgiDevice).ThrowOnFailure();
        _dxgiDevice = dxgiDevice;

        IDXGIFactory2* factory = null;
        Guid iidFactory = IDXGIFactory2.IID_Guid;
        PInvoke.CreateDXGIFactory2(0, &iidFactory, (void**)&factory).ThrowOnFailure();
        var desc = new DXGI_SWAP_CHAIN_DESC1
        {
            Width = (uint)_width,
            Height = (uint)_height,
            Format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM,
            SampleDesc = new DXGI_SAMPLE_DESC { Count = 1, Quality = 0 },
            BufferUsage = DXGI_USAGE.DXGI_USAGE_RENDER_TARGET_OUTPUT,
            BufferCount = 2,
            Scaling = DXGI_SCALING.DXGI_SCALING_STRETCH,
            SwapEffect = DXGI_SWAP_EFFECT.DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL,
            AlphaMode = DXGI_ALPHA_MODE.DXGI_ALPHA_MODE_PREMULTIPLIED,
            Flags = DXGI_SWAP_CHAIN_FLAG.DXGI_SWAP_CHAIN_FLAG_FRAME_LATENCY_WAITABLE_OBJECT,
        };
        IDXGISwapChain1* swapChain1 = null;
        factory->CreateSwapChainForComposition((IUnknown*)device, &desc, null, &swapChain1);
        factory->Release();

        IDXGISwapChain2* swapChain = null;
        Guid iidSwap2 = IDXGISwapChain2.IID_Guid;
        ((IUnknown*)swapChain1)->QueryInterface(&iidSwap2, (void**)&swapChain).ThrowOnFailure();
        swapChain1->Release();
        _swapChain = swapChain;
        _swapChain->SetMaximumFrameLatency(1);
        _frameWait = _swapChain->GetFrameLatencyWaitableObject();

        IDCompositionDevice* dcomp = null;
        Guid iidDcomp = IDCompositionDevice.IID_Guid;
        PInvoke.DCompositionCreateDevice(dxgiDevice, &iidDcomp, (void**)&dcomp).ThrowOnFailure();
        _dcomp = dcomp;
        IDCompositionTarget* target = null;
        _dcomp->CreateTargetForHwnd(hwnd, true, &target);
        _target = target;
        IDCompositionVisual* visual = null;
        _dcomp->CreateVisual(&visual);
        _visual = visual;
        _visual->SetContent((IUnknown*)_swapChain);
        _target->SetRoot(_visual);

        ID2D1Factory1* d2dFactory = null;
        Guid iidD2dFactory = ID2D1Factory1.IID_Guid;
        var factoryOptions = default(D2D1_FACTORY_OPTIONS);
        PInvoke.D2D1CreateFactory(D2D1_FACTORY_TYPE.D2D1_FACTORY_TYPE_SINGLE_THREADED, &iidD2dFactory, &factoryOptions, (void**)&d2dFactory).ThrowOnFailure();
        _d2dFactory = d2dFactory;
        ID2D1Device* d2dDevice = null;
        _d2dFactory->CreateDevice(dxgiDevice, &d2dDevice);
        _d2dDevice = d2dDevice;
        ID2D1DeviceContext* dc = null;
        _d2dDevice->CreateDeviceContext(D2D1_DEVICE_CONTEXT_OPTIONS.D2D1_DEVICE_CONTEXT_OPTIONS_NONE, &dc);
        _dc = dc;
        _dc->SetDpi(96, 96);

        // Flip model: buffer 0 is always the current back buffer, so one target bitmap serves every frame.
        IDXGISurface* surface = null;
        Guid iidSurface = IDXGISurface.IID_Guid;
        _swapChain->GetBuffer(0, &iidSurface, (void**)&surface);
        var targetProps = new D2D1_BITMAP_PROPERTIES1
        {
            pixelFormat = new D2D1_PIXEL_FORMAT { format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, alphaMode = D2D1_ALPHA_MODE.D2D1_ALPHA_MODE_PREMULTIPLIED },
            dpiX = 96,
            dpiY = 96,
            bitmapOptions = D2D1_BITMAP_OPTIONS.D2D1_BITMAP_OPTIONS_TARGET | D2D1_BITMAP_OPTIONS.D2D1_BITMAP_OPTIONS_CANNOT_DRAW,
        };
        ID2D1Bitmap1* backBuffer = null;
        _dc->CreateBitmapFromDxgiSurface(surface, &targetProps, &backBuffer);
        surface->Release();
        _backBuffer = backBuffer;
        _dc->SetTarget((ID2D1Image*)_backBuffer);

        var iconProps = new D2D1_BITMAP_PROPERTIES1
        {
            pixelFormat = new D2D1_PIXEL_FORMAT { format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, alphaMode = D2D1_ALPHA_MODE.D2D1_ALPHA_MODE_PREMULTIPLIED },
            dpiX = 96,
            dpiY = 96,
            bitmapOptions = D2D1_BITMAP_OPTIONS.D2D1_BITMAP_OPTIONS_NONE,
        };
        _icons = new ID2D1Bitmap1*[options.Count];
        for (int i = 0; i < options.Count; i++)
        {
            byte[] pixels = SyntheticIconPixels.Create(i, options.Count);
            ID2D1Bitmap1* icon = null;
            fixed (byte* p = pixels)
            {
                var size = new D2D_SIZE_U { width = SyntheticIconPixels.PixelSize, height = SyntheticIconPixels.PixelSize };
                _dc->CreateBitmap(size, p, SyntheticIconPixels.PixelSize * 4, &iconProps, &icon);
            }

            _icons[i] = icon;
        }

        var black = new D2D1_COLOR_F { r = 0, g = 0, b = 0, a = 1 };
        ID2D1SolidColorBrush* brush = null;
        _dc->CreateSolidColorBrush(&black, null, &brush);
        _black = brush;

        _dcomp->Commit();
    }

    /// <summary>Waits until the swap chain accepts a new frame (at most one queued); false on timeout.</summary>
    public bool WaitForFrame(uint timeoutMs) => PInvoke.WaitForSingleObject(_frameWait, timeoutMs) == WAIT_EVENT.WAIT_OBJECT_0;

    public void Draw(BarLayout layout)
    {
        var clear = new D2D1_COLOR_F { r = 0, g = 0, b = 0, a = 0 };
        _dc->BeginDraw();
        _dc->Clear(&clear);
        if (_options.Backdrop)
        {
            var band = new D2D_RECT_F { left = 0, top = 0, right = _width, bottom = (float)(layout.Top + _options.Base + 16) };
            _dc->FillRectangle(&band, (ID2D1Brush*)_black);
        }

        for (int i = 0; i < _icons.Length; i++)
        {
            float left = (float)layout.Left[i];
            float top = (float)layout.Top;
            float size = (float)layout.Size[i];
            var dest = new D2D_RECT_F { left = left, top = top, right = left + size, bottom = top + size };
            _dc->DrawBitmap(
                (ID2D1Bitmap*)_icons[i], &dest, 1f, D2D1_INTERPOLATION_MODE.D2D1_INTERPOLATION_MODE_HIGH_QUALITY_CUBIC, null, null);
        }

        _dc->EndDraw(null, null);
        _swapChain->Present(1, 0).ThrowOnFailure();
    }

    public void Dispose()
    {
        _black->Release();
        foreach (ID2D1Bitmap1* icon in _icons)
        {
            icon->Release();
        }

        _dc->SetTarget(null);
        _backBuffer->Release();
        _dc->Release();
        _d2dDevice->Release();
        _d2dFactory->Release();
        _visual->Release();
        _target->Release();
        _dcomp->Release();
        PInvoke.CloseHandle(_frameWait);
        _swapChain->Release();
        _dxgiDevice->Release();
        _device->Release();
    }
}

/// <summary>The WindowHost SyntheticIcons discs as raw premultiplied BGRA pixels (no WPF).</summary>
internal static class SyntheticIconPixels
{
    public const int PixelSize = 256;

    private const double Radius = 118.0;
    private const double EdgeSoftness = 6.0;

    public static byte[] Create(int index, int count)
    {
        double hue = 360.0 * index / Math.Max(1, count);
        (double lr, double lg, double lb) = Hsv(hue, 0.30, 1.00);
        (double dr, double dg, double db) = Hsv(hue, 0.95, 0.50);

        const double centre = (PixelSize - 1) / 2.0;
        const double highlightX = 96.0;
        const double highlightY = 84.0;
        const double gradientReach = 170.0;

        var pixels = new byte[PixelSize * PixelSize * 4];
        for (int y = 0; y < PixelSize; y++)
        {
            for (int x = 0; x < PixelSize; x++)
            {
                double distance = Math.Sqrt(((x - centre) * (x - centre)) + ((y - centre) * (y - centre)));
                double alpha = Math.Clamp(((Radius - distance) / EdgeSoftness) + 0.5, 0.0, 1.0);
                int offset = ((y * PixelSize) + x) * 4;
                if (alpha <= 0.0)
                {
                    continue;
                }

                double hx = x - highlightX;
                double hy = y - highlightY;
                double t = Math.Clamp(Math.Sqrt((hx * hx) + (hy * hy)) / gradientReach, 0.0, 1.0);
                double r = lr + ((dr - lr) * t);
                double g = lg + ((dg - lg) * t);
                double b = lb + ((db - lb) * t);
                pixels[offset] = (byte)Math.Round(b * alpha * 255.0);
                pixels[offset + 1] = (byte)Math.Round(g * alpha * 255.0);
                pixels[offset + 2] = (byte)Math.Round(r * alpha * 255.0);
                pixels[offset + 3] = (byte)Math.Round(alpha * 255.0);
            }
        }

        return pixels;
    }

    private static (double R, double G, double B) Hsv(double hueDegrees, double saturation, double value)
    {
        double h = (hueDegrees % 360.0) / 60.0;
        int sector = (int)Math.Floor(h);
        double f = h - sector;
        double p = value * (1.0 - saturation);
        double q = value * (1.0 - (saturation * f));
        double t = value * (1.0 - (saturation * (1.0 - f)));
        return sector switch
        {
            0 => (value, t, p),
            1 => (q, value, p),
            2 => (p, value, t),
            3 => (p, q, value),
            4 => (t, p, value),
            _ => (value, p, q),
        };
    }
}
