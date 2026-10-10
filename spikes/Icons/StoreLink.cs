using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.Shell.Common;

namespace MehrBar.Spikes.Icons;

/// <summary>
/// Creates a .lnk that points at a Store app by its AUMID: CoCreateInstance(ShellLink) ->
/// IShellLinkW.SetIDList(SHParseDisplayName("shell:AppsFolder\AUMID")) -> IPersistFile.Save. Such a link has
/// no file-system target, only a shell item ID list, which is exactly the case the bar has to display.
/// </summary>
internal static unsafe class StoreLink
{
    private static readonly Guid ClsidShellLink = new("00021401-0000-0000-C000-000000000046");

    /// <summary>Returns null on success, otherwise a short error code (no paths).</summary>
    public static string? Create(string lnkPath, string aumid)
    {
        string? error = null;
        var thread = new Thread(() => error = CreateOnSta(lnkPath, aumid)) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return error;
    }

    private static string? CreateOnSta(string lnkPath, string aumid)
    {
        bool uninitialize = PInvoke.CoInitializeEx(null, COINIT.COINIT_APARTMENTTHREADED).Succeeded;
        ITEMIDLIST* pidl = null;
        IShellLinkW* link = null;
        IPersistFile* persist = null;
        try
        {
            HRESULT hr;
            fixed (char* name = "shell:AppsFolder\\" + aumid)
            {
                hr = PInvoke.SHParseDisplayName(name, null, &pidl, 0, null);
            }

            if (hr.Failed || pidl == null)
            {
                return "parse=0x" + ((uint)hr.Value).ToString("X8");
            }

            Guid clsid = ClsidShellLink;
            Guid iidLink = IShellLinkW.IID_Guid;
            void* raw = null;
            hr = PInvoke.CoCreateInstance(&clsid, null, CLSCTX.CLSCTX_INPROC_SERVER, &iidLink, &raw);
            if (hr.Failed || raw == null)
            {
                return "create=0x" + ((uint)hr.Value).ToString("X8");
            }

            link = (IShellLinkW*)raw;
            link->SetIDList(pidl);

            Guid iidPersist = IPersistFile.IID_Guid;
            void* rawPersist = null;
            hr = ((IUnknown*)link)->QueryInterface(&iidPersist, &rawPersist);
            if (hr.Failed || rawPersist == null)
            {
                return "persist=0x" + ((uint)hr.Value).ToString("X8");
            }

            persist = (IPersistFile*)rawPersist;
            fixed (char* target = lnkPath)
            {
                persist->Save(target, true);
            }

            return null;
        }
        catch (COMException ce)
        {
            return "hr=0x" + ((uint)ce.HResult).ToString("X8");
        }
        catch (Exception ex)
        {
            return "ex=" + ex.GetType().Name;
        }
        finally
        {
            if (persist != null)
            {
                persist->Release();
            }

            if (link != null)
            {
                link->Release();
            }

            if (pidl != null)
            {
                PInvoke.CoTaskMemFree(pidl);
            }

            if (uninitialize)
            {
                PInvoke.CoUninitialize();
            }
        }
    }
}
