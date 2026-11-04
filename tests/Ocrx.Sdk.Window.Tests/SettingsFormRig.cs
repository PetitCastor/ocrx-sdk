using System.Collections.Concurrent;

namespace Ocrx.Sdk.Window.Tests;

/// <summary>A <see cref="SettingsForm"/> shown by a host on its own STA thread, with every edit recorded.</summary>
internal sealed class SettingsFormRig : IDisposable
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(15);

    private readonly HostRig _rig;
    private readonly ConcurrentQueue<SettingsEdit> _edits = new();

    public SettingsFormRig(IReadOnlyList<SettingsField> fields, Func<SettingsEdit, Task<SettingsResult>>? onEdit = null)
        : this(() => fields, onEdit)
    {
    }

    public SettingsFormRig(
        Func<IReadOnlyList<SettingsField>> fields, Func<SettingsEdit, Task<SettingsResult>>? onEdit = null)
    {
        _rig = new HostRig(() => new SettingsForm("Test", fields, async edit =>
        {
            _edits.Enqueue(edit);
            return onEdit is null ? SettingsResult.Refresh(fields()) : await onEdit(edit);
        }));
    }

    public SettingsEdit[] Edits => [.. _edits];

    public HostRig Host => _rig;

    public async Task ShowAsync() => await _rig.Host.ShowAsync().WaitAsync(Limit);

    /// <summary>Reads or changes the form on its own thread, as WinForms requires.</summary>
    public T Ui<T>(Func<SettingsForm, T> read) => _rig.OnForm(form => read((SettingsForm)form));

    public T Input<T>(string id) where T : Control => Ui(form => (T)form.InputOf(id));

    /// <summary>Changes a control, waits for the edit its own event started, then commits explicitly.</summary>
    public async Task EditAsync(string id, Action<SettingsForm> change)
    {
        Ui(form => { change(form); return 0; });
        await Ui(form => form.LastCommit).WaitAsync(Limit);
        await CommitAsync(id);
    }

    /// <summary>Sends every unsent edit, as a deactivation or a close would, and waits for it.</summary>
    public async Task CommitDirtyAsync()
    {
        var task = (Task)_rig.Form!.Invoke((Func<Task>)(() => ((SettingsForm)_rig.Form!).CommitDirty()));
        await task.WaitAsync(Limit);
    }

    public async Task CommitAsync(string id)
    {
        var task = (Task)_rig.Form!.Invoke((Func<Task>)(() => ((SettingsForm)_rig.Form!).CommitAsync(id)));
        await task.WaitAsync(Limit);
    }

    public void Dispose() => _rig.Dispose();
}
