using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Ocrx.Sdk.Window;

internal sealed class Win32WindowPlatform : IWindowPlatform
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const int DwmwaUseImmersiveDarkMode = 20;
    // NOSIZE NOMOVE NOZORDER NOACTIVATE FRAMECHANGED
    private const uint SwpFrameUnchangedElsewhere = 0x0001 | 0x0002 | 0x0004 | 0x0010 | 0x0020;

    internal static bool ReadAppsAreDark()
    {
        try
        {
            // The value is absent on installs that never changed it, which means light.
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    public bool SystemAppsAreDark() => ReadAppsAreDark();

    /// <summary>
    /// WinForms always draws the title bar light; Windows 10 20H1 and later draw it dark on request and
    /// older builds ignore the request. A window already shown keeps its old bar until its frame is
    /// redrawn, hence the SetWindowPos.
    /// </summary>
    public void ApplyTitleBar(IntPtr handle, bool dark)
    {
        var on = dark ? 1 : 0;
        _ = DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref on, sizeof(int));
        _ = SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0, SwpFrameUnchangedElsewhere);
    }

    /// <summary>Without the engine's AllowSetForegroundWindow grant Windows only flashes the taskbar button.</summary>
    public void BringToFront(Form form)
    {
        _ = SetForegroundWindow(form.Handle);
        form.Activate();
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hwnd);
}
