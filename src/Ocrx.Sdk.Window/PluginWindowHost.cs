using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Ocrx.Sdk.Window;

/// <summary>
/// Runs a plugin's own window on a background STA thread of its own, so the plugin's host loop never
/// waits on the window and the window never waits on it. <see cref="ShowAsync"/> builds the form on
/// first use and otherwise restores and activates it; closing the window hides it; <see cref="Dispose"/>
/// closes it for real and ends the thread.
/// </summary>
public sealed class PluginWindowHost : IDisposable
{
    // Long enough for a form that is busy closing, short enough that a plugin shutting down is not stuck.
    private static readonly TimeSpan StopWait = TimeSpan.FromSeconds(5);

    private readonly Func<Form> _createForm;
    private readonly Action<string> _log;
    private readonly IWindowPlatform _platform;
    private readonly string _threadName;
    private readonly object _gate = new();
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Thread? _thread;
    private Control? _ui;
    private Form? _form;
    private volatile bool _disposed;
    private volatile bool _closing;
    private volatile Icon? _icon;
    private int _theme;

    /// <param name="createForm">
    /// Builds the window. Runs on the host's STA thread, once, and again only if the form was disposed.
    /// </param>
    /// <param name="log">Receives faults from the window thread; they are never shown as a dialog.</param>
    /// <param name="threadName">Name of the UI thread, for debuggers.</param>
    public PluginWindowHost(Func<Form> createForm, Action<string>? log = null, string? threadName = null)
        : this(createForm, new Win32WindowPlatform(), log, threadName)
    {
    }

    internal PluginWindowHost(
        Func<Form> createForm, IWindowPlatform platform, Action<string>? log = null, string? threadName = null)
    {
        ArgumentNullException.ThrowIfNull(createForm);
        _createForm = createForm;
        _platform = platform;
        _log = log ?? (_ => { });
        _threadName = threadName ?? "Plugin window";
    }

    /// <summary>
    /// The window's theme. Set it from any thread; the window is recoloured at once. The host sets the
    /// form's BackColor and ForeColor on every apply, so a custom form should not set its own top-level
    /// colours; it can read this to colour its child controls.
    /// </summary>
    public WindowTheme Theme
    {
        get => (WindowTheme)Volatile.Read(ref _theme);
        set
        {
            Volatile.Write(ref _theme, (int)value);
            OnUi(() => ApplyTheme(_form));
        }
    }

    /// <summary>
    /// The window icon, which Windows also uses for the title bar and the taskbar. The host does not
    /// own it: the caller keeps it alive and disposes it.
    /// </summary>
    public Icon? Icon
    {
        get => _icon;
        set
        {
            _icon = value;
            OnUi(() => ApplyIcon(_form));
        }
    }

    /// <summary>
    /// Shows the window and brings it to the front, building it first if this is the first call. The
    /// task faults if the factory throws, and completes quietly once the host is disposed.
    /// </summary>
    public async Task ShowAsync()
    {
        // The thread starts here, not in the constructor: a plugin whose user never opens the window pays nothing.
        lock (_gate)
        {
            if (_disposed) return;
            if (_thread is null)
            {
                _thread = new Thread(Run) { IsBackground = true, Name = _threadName };
                _thread.SetApartmentState(ApartmentState.STA);
                _thread.Start();
            }
        }

        await _ready.Task.ConfigureAwait(false);
        var ui = _ui ?? throw new InvalidOperationException("The plugin window thread could not start.");

        var shown = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            ui.BeginInvoke(() =>
            {
                try
                {
                    OpenForm();
                    shown.TrySetResult();
                }
                catch (Exception ex)
                {
                    shown.TrySetException(ex);
                }
            });
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            return; // Disposed between the check and the call.
        }

        // The thread can end with this call still queued; _stopped keeps the caller from waiting for it.
        var done = await Task.WhenAny(shown.Task, _stopped.Task).ConfigureAwait(false);
        if (done == shown.Task) await shown.Task.ConfigureAwait(false);
    }

    private void Run()
    {
        Control ui;
        try
        {
            // A fault on this thread is logged, never the WinForms dialog: nobody watches a plugin's
            // background window. Thread scope, because the application-wide mode throws once any
            // window exists, and the SDK overlay may already have made one.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException, threadScope: true);
            Application.ThreadException += (_, e) => _log($"The plugin window failed: {e.Exception.Message}");
            Application.EnableVisualStyles();

            // An invisible control whose handle marshals calls onto this thread, form built or not.
            // Reading Handle is what creates it; a parentless control never shown would not otherwise.
            ui = new Control();
            _ = ui.Handle;
        }
        catch (Exception ex)
        {
            _log($"The plugin window could not start: {ex.Message}");
            _ready.TrySetResult();
            _stopped.TrySetResult();
            return;
        }

        _ui = ui;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        _ready.TrySetResult();
        try
        {
            Application.Run();
        }
        finally
        {
            // Whatever ended the loop, the caller waiting on _stopped must be released.
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            try
            {
                _form?.Dispose();
                ui.Dispose();
            }
            finally
            {
                _stopped.TrySetResult();
            }
        }
    }

    private void OpenForm()
    {
        // Dispose has started: do not build or show a window that is about to be closed.
        if (_closing) return;

        if (_form is null || _form.IsDisposed)
        {
            var form = _createForm();
            // Subscribed from outside because the form is the plugin's, not ours to subclass.
            form.FormClosing += OnFormClosing;
            form.HandleCreated += (_, _) => _platform.ApplyTitleBar(form.Handle, IsDark());
            _form = form;
            ApplyTheme(form);
            ApplyIcon(form);
        }

        var shown = _form;
        if (!shown.Visible) shown.Show();
        if (shown.WindowState == FormWindowState.Minimized) shown.WindowState = FormWindowState.Normal;
        _platform.BringToFront(shown);
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        // The user's close only hides the window so reopening is instant and keeps its state; the end
        // of the run, or Windows shutting down, closes it for real.
        if (!_closing && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            ((Form)sender!).Hide();
        }
    }

    private bool IsDark() => WindowThemes.IsDark(Theme, _platform.SystemAppsAreDark);

    private void ApplyTheme(Form? form)
    {
        if (form is null || form.IsDisposed) return;

        var dark = IsDark();
        form.BackColor = ThemeColors.Back(dark);
        form.ForeColor = ThemeColors.Fore(dark);
        if (form.IsHandleCreated) _platform.ApplyTitleBar(form.Handle, dark);
    }

    private void ApplyIcon(Form? form)
    {
        if (form is null || form.IsDisposed || _icon is not { } icon) return;
        form.Icon = icon;
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        // Raised on SystemEvents' own thread; the window is touched only on ours.
        if (Theme == WindowTheme.System) OnUi(() => ApplyTheme(_form));
    }

    /// <summary>Runs on the UI thread; dropped when the thread never started or is gone.</summary>
    private void OnUi(Action action)
    {
        var ui = _ui;
        if (ui is null || !ui.IsHandleCreated) return;

        try
        {
            ui.BeginInvoke(action);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            // The thread ended between the check and the call; a window going away has nothing left to show.
        }
    }

    /// <summary>Closes the window for real and ends the thread. Safe to call more than once.</summary>
    public void Dispose()
    {
        Thread? thread;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            thread = _thread;
        }

        if (thread is null) return;

        // The thread may still be building its marshalling control; a close queued before it exists would be dropped.
        _ready.Task.Wait(StopWait);

        // Set before the close is queued, so FormClosing lets it through.
        _closing = true;
        OnUi(() =>
        {
            try
            {
                _form?.Close();
            }
            finally
            {
                // A form that throws while closing must not leave the thread running.
                Application.ExitThread();
            }
        });

        if (thread != Thread.CurrentThread && thread.IsAlive && !thread.Join(StopWait))
            _log("The plugin window thread did not stop in time.");
    }
}
