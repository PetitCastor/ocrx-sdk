using System.Drawing;
using System.Windows.Forms;

namespace Ocrx.Sdk.Window.Tests;

internal sealed class HostRig : IDisposable
{
    private int _created;

    public FakeWindowPlatform Platform { get; } = new();

    public Form? Form { get; private set; }

    public Thread? FormThread { get; private set; }

    public int Created => Volatile.Read(ref _created);

    public PluginWindowHost Host { get; }

    public HostRig(Func<Form>? factory = null)
    {
        Host = new PluginWindowHost(
            () =>
            {
                Interlocked.Increment(ref _created);
                FormThread = Thread.CurrentThread;
                return Form = Invisible(factory is null ? new Form() : factory());
            },
            Platform);
    }

    /// <summary>
    /// Tests must never show a window on the desktop of whoever runs them: off every screen, fully
    /// transparent (also when minimised and restored), and out of the taskbar.
    /// </summary>
    private static Form Invisible(Form form)
    {
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-32000, -32000);
        form.ShowInTaskbar = false;
        form.Opacity = 0;
        return form;
    }

    /// <summary>Reads or changes the form on its own thread, as WinForms requires.</summary>
    public T OnForm<T>(Func<Form, T> read) => (T)Form!.Invoke(() => read(Form!))!;

    public void Dispose() => Host.Dispose();
}
