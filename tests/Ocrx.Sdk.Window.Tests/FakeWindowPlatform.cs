namespace Ocrx.Sdk.Window.Tests;

internal sealed class FakeWindowPlatform : IWindowPlatform
{
    private readonly object _gate = new();
    private readonly List<bool> _titleBars = [];
    private int _foregroundCalls;

    public bool SystemDark { get; set; }

    public int ForegroundCalls
    {
        get { lock (_gate) return _foregroundCalls; }
    }

    public bool[] TitleBars
    {
        get { lock (_gate) return [.. _titleBars]; }
    }

    public bool SystemAppsAreDark() => SystemDark;

    public void ApplyTitleBar(IntPtr handle, bool dark)
    {
        lock (_gate) _titleBars.Add(dark);
    }

    public void BringToFront(Form form)
    {
        lock (_gate) _foregroundCalls++;
    }
}
