namespace Ocrx.Sdk.Window;

/// <summary>The Win32 calls the host makes, behind a seam so its logic runs without a desktop.</summary>
internal interface IWindowPlatform
{
    bool SystemAppsAreDark();

    void ApplyTitleBar(IntPtr handle, bool dark);

    /// <summary>Makes the (shown, restored) form the foreground window and activates it.</summary>
    void BringToFront(Form form);
}
