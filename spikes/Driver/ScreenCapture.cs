using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Direct3D;
using Windows.Win32.Graphics.Direct3D11;
using Windows.Win32.Graphics.Dxgi;
using Windows.Win32.Graphics.Dxgi.Common;
using Windows.Win32.System.Com;

namespace MehrBar.Spikes.Driver;

/// <summary>
/// Screen-side frame capture (plan 01-19) with the DXGI Desktop Duplication API: every frame the DWM composes for the
/// output is acquired with its present time; for frames whose image changed, a few pixel rows of a screen rectangle
/// are copied out. This measures what is on screen, also for layered windows that PresentMon does not see (01-16, A3),
/// and for any other program (RocketDock). Read-only: it never touches input or windows.
/// </summary>
[SupportedOSPlatform("windows8.0")]
internal sealed unsafe class ScreenCapture : IDisposable
{
    private readonly int _x;
    private readonly int _width;
    private readonly int[] _rows;
    private readonly List<CapturedFrame> _frames = [];
    private Thread? _thread;
    private volatile bool _stop;

    public ScreenCapture(int x, int width, int[] rows)
    {
        _x = x;
        _width = width;
        _rows = rows;
    }

    /// <summary>Captured frames whose image changed anywhere on the output (DXGI LastPresentTime != 0).</summary>
    public IReadOnlyList<CapturedFrame> Frames => _frames;

    public string? Error { get; private set; }

    public int Width => _width;

    public int RowCount => _rows.Length;

    public void Start()
    {
        using var ready = new ManualResetEventSlim();
        _thread = new Thread(() => Loop(ready)) { IsBackground = true, Name = "screen-capture", Priority = ThreadPriority.AboveNormal };
        _thread.Start();
        ready.Wait(5000);
    }

    public void Stop()
    {
        _stop = true;
        _thread?.Join(5000);
    }

    public void Dispose() => Stop();

    private void Loop(ManualResetEventSlim ready)
    {
        ID3D11Device* device = null;
        ID3D11DeviceContext* context = null;
        IDXGIOutputDuplication* duplication = null;
        ID3D11Texture2D* staging = null;
        try
        {
            HRESULT hr = PInvoke.D3D11CreateDevice(
                null, D3D_DRIVER_TYPE.D3D_DRIVER_TYPE_HARDWARE, HMODULE.Null, 0, null, 0, PInvoke.D3D11_SDK_VERSION,
                &device, null, &context);
            hr.ThrowOnFailure();

            duplication = Duplicate(device, out int originX, out int originY);

            var desc = new D3D11_TEXTURE2D_DESC
            {
                Width = (uint)_width,
                Height = (uint)_rows.Length,
                MipLevels = 1,
                ArraySize = 1,
                Format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM,
                SampleDesc = new DXGI_SAMPLE_DESC { Count = 1, Quality = 0 },
                Usage = D3D11_USAGE.D3D11_USAGE_STAGING,
                BindFlags = 0,
                CPUAccessFlags = D3D11_CPU_ACCESS_FLAG.D3D11_CPU_ACCESS_READ,
                MiscFlags = 0,
            };
            device->CreateTexture2D(&desc, null, &staging);

            ready.Set();
            double ticksToMs = 1000.0 / Stopwatch.Frequency;
            int rowBytes = _width * 4;
            while (!_stop)
            {
                DXGI_OUTDUPL_FRAME_INFO info;
                IDXGIResource* resource = null;
                try
                {
                    duplication->AcquireNextFrame(50, &info, &resource);
                }
                catch (COMException ex) when (ex.HResult == HRESULT.DXGI_ERROR_WAIT_TIMEOUT.Value)
                {
                    continue;
                }

                bool imageUpdated = info.LastPresentTime != 0;
                if (imageUpdated)
                {
                    ID3D11Texture2D* texture = null;
                    Guid iid = ID3D11Texture2D.IID_Guid;
                    ((IUnknown*)resource)->QueryInterface(&iid, (void**)&texture).ThrowOnFailure();
                    for (int r = 0; r < _rows.Length; r++)
                    {
                        var box = new D3D11_BOX
                        {
                            left = (uint)(_x - originX),
                            right = (uint)(_x - originX + _width),
                            top = (uint)(_rows[r] - originY),
                            bottom = (uint)(_rows[r] - originY + 1),
                            front = 0,
                            back = 1,
                        };
                        context->CopySubresourceRegion((ID3D11Resource*)staging, 0, 0, (uint)r, 0, (ID3D11Resource*)texture, 0, &box);
                    }

                    texture->Release();
                }

                resource->Release();
                duplication->ReleaseFrame();
                if (!imageUpdated)
                {
                    continue;
                }

                D3D11_MAPPED_SUBRESOURCE mapped;
                context->Map((ID3D11Resource*)staging, 0, D3D11_MAP.D3D11_MAP_READ, 0, &mapped);
                var pixels = new byte[rowBytes * _rows.Length];
                fixed (byte* dst = pixels)
                {
                    for (int r = 0; r < _rows.Length; r++)
                    {
                        Buffer.MemoryCopy((byte*)mapped.pData + (r * mapped.RowPitch), dst + (r * rowBytes), rowBytes, rowBytes);
                    }
                }

                context->Unmap((ID3D11Resource*)staging, 0);
                _frames.Add(new CapturedFrame(info.LastPresentTime * ticksToMs, info.AccumulatedFrames, pixels));
            }
        }
        catch (Exception ex)
        {
            Error = ex.GetType().Name + ": " + ex.Message;
        }
        finally
        {
            ready.Set();
            if (staging != null)
            {
                staging->Release();
            }

            if (duplication != null)
            {
                duplication->Release();
            }

            if (context != null)
            {
                context->Release();
            }

            if (device != null)
            {
                device->Release();
            }
        }
    }

    /// <summary>Duplicates the output whose desktop rectangle contains the capture rows.</summary>
    private IDXGIOutputDuplication* Duplicate(ID3D11Device* device, out int originX, out int originY)
    {
        originX = 0;
        originY = 0;
        IDXGIDevice* dxgiDevice = null;
        IDXGIAdapter* adapter = null;
        try
        {
            Guid iidDevice = IDXGIDevice.IID_Guid;
            ((IUnknown*)device)->QueryInterface(&iidDevice, (void**)&dxgiDevice).ThrowOnFailure();
            dxgiDevice->GetAdapter(&adapter);
            for (uint i = 0; ; i++)
            {
                IDXGIOutput* output = null;
                HRESULT hr = adapter->EnumOutputs(i, &output);
                if (hr == HRESULT.DXGI_ERROR_NOT_FOUND)
                {
                    break;
                }

                hr.ThrowOnFailure();
                DXGI_OUTPUT_DESC desc = output->GetDesc();
                RECT bounds = desc.DesktopCoordinates;
                bool contains = _x >= bounds.left && _x + _width <= bounds.right &&
                                _rows.Min() >= bounds.top && _rows.Max() < bounds.bottom;
                if (!contains)
                {
                    output->Release();
                    continue;
                }

                IDXGIOutput1* output1 = null;
                Guid iidOutput1 = IDXGIOutput1.IID_Guid;
                ((IUnknown*)output)->QueryInterface(&iidOutput1, (void**)&output1).ThrowOnFailure();
                output->Release();
                IDXGIOutputDuplication* duplication = null;
                output1->DuplicateOutput((IUnknown*)device, &duplication);
                output1->Release();
                originX = bounds.left;
                originY = bounds.top;
                return duplication;
            }
        }
        finally
        {
            if (adapter != null)
            {
                adapter->Release();
            }

            if (dxgiDevice != null)
            {
                dxgiDevice->Release();
            }
        }

        throw new InvalidOperationException("no output contains the capture rectangle");
    }
}

/// <summary>One composed frame: QPC present time in ms, frames accumulated since the last acquire, captured rows (BGRA).</summary>
internal sealed record CapturedFrame(double QpcMs, uint Accumulated, byte[] Pixels);
