using System.Globalization;

namespace Ocrx.Sdk.Window;

/// <summary>
/// A settings window drawn from field declarations, for <see cref="PluginWindowHost"/> or any other host.
/// Each committed edit goes to the plugin's callback, which validates, applies and persists it and answers
/// with <see cref="SettingsResult.Refresh"/> (the fields to show now) or <see cref="SettingsResult.Fail"/>
/// (the field reverts and shows the message). The callback runs on the form's UI thread, one edit at a time:
/// an edit made while another is running is sent when that one finishes. Edits not yet sent (a text box
/// typed in but not left) are sent when the window is deactivated or closed, and survive a refresh.
/// </summary>
public sealed class SettingsForm : Form
{
    private const int ContentWidth = 440;

    // What a group box takes from the inputs inside it: its padding on both sides and its border.
    private const int GroupInset = 28;

    private readonly Func<IReadOnlyList<SettingsField>> _fields;
    private readonly Func<SettingsEdit, Task<SettingsResult>> _onEdit;
    private readonly FlowLayoutPanel _content = new()
    {
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        Padding = new Padding(16),
    };
    private readonly Dictionary<string, FieldRow> _rows = [];

    // Controls that do not take the form's colours on their own, and the labels drawn dimmer than the text.
    private readonly List<Control> _inputs = [];
    private readonly List<Label> _muted = [];
    private readonly HashSet<string> _waiting = [];
    private bool _coloured;
    private bool _loading;
    private bool _busy;
    private bool _shownOnce;
    private int _width = ContentWidth;

    /// <param name="title">The window title.</param>
    /// <param name="fields">
    /// What to draw, top to bottom; ids must be unique. Read now and each time the window is shown again, so
    /// it reflects the plugin's current values. It runs on the UI thread and must not block.
    /// </param>
    /// <param name="onEdit">
    /// Applies one committed edit; see <see cref="SettingsResult"/>. It runs on the UI thread and must not block.
    /// </param>
    public SettingsForm(
        string title, Func<IReadOnlyList<SettingsField>> fields, Func<SettingsEdit, Task<SettingsResult>> onEdit)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(onEdit);
        _fields = fields;
        _onEdit = onEdit;

        Text = title;
        AutoScroll = true;
        ClientSize = new Size(500, 560);
        MinimumSize = new Size(420, 300);
        StartPosition = FormStartPosition.CenterScreen;
        // Not docked: a docked child adds nothing to the scroll range.
        Controls.Add(_content);
        var initial = fields();
        Validate(initial);
        Build(initial);
    }

    /// <summary>The newest commit a control's own event started, so a test can wait for it.</summary>
    internal Task LastCommit { get; private set; } = Task.CompletedTask;

    internal Control InputOf(string id) => _rows[id].Input;

    internal string? ErrorOf(string id) => _rows[id].Error.Visible ? _rows[id].Error.Text : null;

    /// <summary>
    /// Everything that makes a form undrawable, checked before any control is built or torn down. A slider's
    /// range must be a whole number of steps, so every position is a value the plugin can name.
    /// </summary>
    internal static void Validate(IReadOnlyList<SettingsField> fields)
    {
        var ids = new HashSet<string>();
        foreach (var field in fields) ValidateField(field, ids, inGroup: false);
    }

    /// <summary>
    /// Sends the field's current value to the plugin, unless it is unchanged. While an edit runs, the id is
    /// remembered and committed again once that edit is done, so edits never overlap.
    /// </summary>
    internal async Task CommitAsync(string id)
    {
        if (_busy)
        {
            _waiting.Add(id);
            return;
        }

        if (!_rows.TryGetValue(id, out var row)) return;

        var value = row.Read();
        if (Equals(value, row.Good)) return;

        _busy = true;
        try
        {
            SettingsResult result;
            try
            {
                result = await _onEdit(new SettingsEdit(id, value));
            }
            catch (Exception ex)
            {
                // A plugin bug must not close its window; the user sees what went wrong next to the field.
                result = SettingsResult.Fail(ex.Message);
            }

            // A callback that finishes at once would otherwise rebuild, and so dispose, the control whose
            // change event is still on the stack. Yielding lets that event return first.
            await Task.Yield();
            if (IsDisposed) return;

            if (result.Fields is { } offered)
            {
                // Checked before anything is torn down: a form the plugin cannot draw is a failed edit.
                try
                {
                    Validate(offered);
                }
                catch (ArgumentException ex)
                {
                    result = SettingsResult.Fail(ex.Message);
                }
            }

            if (result.Error is { } message)
            {
                Quietly(() => row.Show(row.Good));
                row.Error.Text = message;
                row.Error.Visible = true;
            }
            else
            {
                row.Good = value;
                foreach (var other in _rows.Values) other.Error.Visible = false;
                if (result.Fields is { } fields) Reload(fields);
            }
        }
        finally
        {
            _busy = false;
        }

        var queued = _waiting.ToArray();
        _waiting.Clear();
        foreach (var queuedId in queued) await CommitAsync(queuedId);
    }

    /// <summary>Sends every edit not yet sent: fields whose control no longer matches the last accepted value.</summary>
    internal Task CommitDirty()
    {
        return Task.WhenAll(Unsent().Keys.Select(CommitAsync));
    }

    /// <summary>
    /// The fields whose control no longer matches the last accepted value, with the control's value. Quiet:
    /// reading a NumericUpDown can raise its change event, which must not start a commit of its own.
    /// </summary>
    private Dictionary<string, object> Unsent()
    {
        var before = _loading;
        _loading = true;
        try
        {
            return _rows
                .Select(r => (r.Key, Value: r.Value.Read(), r.Value.Good))
                .Where(r => !Equals(r.Value, r.Good))
                .ToDictionary(r => r.Key, r => r.Value);
        }
        finally
        {
            _loading = before;
        }
    }

    /// <inheritdoc />
    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        LastCommit = CommitDirty();
    }

    /// <inheritdoc />
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);

        // Only the hide-close (a host cancelled the user's close): the window stays, the user is done with
        // what they typed. A real close, such as the host disposing, has no plugin left to hear it.
        if (e.Cancel && e.CloseReason == CloseReason.UserClosing) LastCommit = CommitDirty();
    }

    /// <inheritdoc />
    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (!Visible) return;

        // The first show uses what the constructor read; later ones may find the plugin's values changed.
        var reload = _shownOnce && !_busy;
        _shownOnce = true;
        if (!reload) return;

        IReadOnlyList<SettingsField> fresh;
        try
        {
            fresh = _fields();
            Validate(fresh);
        }
        catch (Exception)
        {
            // Nobody can answer for this refresh, and the window as it was is still usable; raising here
            // would only reach the thread's exception handler.
            return;
        }

        Reload(fresh);
    }

    /// <inheritdoc />
    protected override void OnBackColorChanged(EventArgs e)
    {
        base.OnBackColorChanged(e);
        _coloured = true;
        ApplyColors();
    }

    /// <inheritdoc />
    protected override void OnForeColorChanged(EventArgs e)
    {
        base.OnForeColorChanged(e);
        _coloured = true;
        ApplyColors();
    }

    private static void ValidateField(SettingsField field, HashSet<string> ids, bool inGroup)
    {
        switch (field)
        {
            case GroupField group:
                if (inGroup) throw new ArgumentException($"Group {group.Id}: groups do not nest.");
                foreach (var child in group.Fields) ValidateField(child, ids, inGroup: true);
                break;
            case HelpField:
                break;
            case IntField i:
                NewId(ids, i.Id);
                ValidateNumber(i.Id, i.Min, i.Max, i.Step);
                break;
            case FloatField f:
                NewId(ids, f.Id);
                ValidateNumber(f.Id, f.Min, f.Max, f.Step);
                break;
            case SelectField or ToggleField or StringField:
                NewId(ids, field.Id);
                break;
            default:
                throw new ArgumentException($"Unsupported field type {field.GetType().Name}.");
        }
    }

    private static void NewId(HashSet<string> ids, string id)
    {
        if (!ids.Add(id)) throw new ArgumentException($"Duplicate field id {id}.");
    }

    private static void ValidateNumber(string id, double? min, double? max, double step)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(step, 0, $"Field {id}: Step");
        if (min is not { } low || max is not { } high) return;

        if (high <= low) throw new ArgumentException($"Field {id}: Max must be above Min.");

        var steps = (high - low) / step;
        if (Math.Abs(steps - Math.Round(steps)) > 1e-6)
            throw new ArgumentException($"Field {id}: Max - Min must be a whole number of Steps.");
    }

    // TextBox, ComboBox, NumericUpDown and TrackBar keep their system colours; everything else inherits.
    private void ApplyColors()
    {
        // Until someone sets a colour the form is in system colours and so are its inputs.
        if (!_coloured) return;

        foreach (var input in _inputs)
        {
            input.BackColor = BackColor;
            input.ForeColor = ForeColor;
        }

        var muted = Color.FromArgb(
            (BackColor.R + ForeColor.R) / 2, (BackColor.G + ForeColor.G) / 2, (BackColor.B + ForeColor.B) / 2);
        foreach (var label in _muted) label.ForeColor = muted;
    }

    /// <summary>Shows new fields, keeping the edits the user has typed but not yet sent, which are then sent.</summary>
    private void Reload(IReadOnlyList<SettingsField> fields)
    {
        Validate(fields);
        var unsent = Unsent();
        Rebuild(fields);
        foreach (var (id, typed) in unsent)
        {
            if (!_rows.TryGetValue(id, out var row)) continue;

            try
            {
                Quietly(() => row.Show(typed));
            }
            catch (Exception ex) when (ex is InvalidCastException or FormatException)
            {
                // The plugin changed what kind of field this is; its new value stands.
            }
        }

        LastCommit = CommitDirty();
    }

    private void Rebuild(IReadOnlyList<SettingsField> fields)
    {
        var scrolled = -AutoScrollPosition.Y;
        // The field the user is in, which need not be the one just edited (they may have tabbed on).
        var focused = _rows
            .FirstOrDefault(r => r.Value.Input == ActiveControl || r.Value.Input.Contains(ActiveControl)).Key;
        SuspendLayout();
        var old = _content.Controls.Cast<Control>().ToArray();
        _content.Controls.Clear();
        // Disposing a focused TextBox raises Leave, which would commit against a row about to be dropped.
        Quietly(() =>
        {
            foreach (var control in old) control.Dispose();
            _rows.Clear();
        });
        _inputs.Clear();
        _muted.Clear();
        Build(fields);
        ResumeLayout(performLayout: true);
        // The scroll range comes from the laid-out content; setting a position before it exists clamps it.
        _content.PerformLayout();
        PerformLayout();
        // Focus first: selecting a control scrolls it into view, and the scroll position must win.
        if (focused is not null && _rows.TryGetValue(focused, out var row)) row.Input.Select();
        AutoScrollPosition = new Point(0, scrolled);
    }

    private void Build(IReadOnlyList<SettingsField> fields)
    {
        foreach (var field in fields) AddField(_content, field);
        ApplyColors();
    }

    private void AddField(Control parent, SettingsField field)
    {
        switch (field)
        {
            case GroupField group:
                var box = new GroupBox
                {
                    Text = group.Label,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    Padding = new Padding(8),
                    Margin = new Padding(0, 0, 0, 12),
                };
                var inner = NewColumn();
                inner.Dock = DockStyle.Top;
                // Narrower inside the box, or the box outgrows the form and a horizontal scrollbar appears.
                _width = ContentWidth - GroupInset;
                foreach (var child in group.Fields) AddField(inner, child);
                _width = ContentWidth;

                box.Controls.Add(inner);
                parent.Controls.Add(box);
                break;
            case HelpField help:
                parent.Controls.Add(NewLabel(help.Text, muted: true));
                break;
            default:
                AddEditable(parent, field);
                break;
        }
    }

    private void AddEditable(Control parent, SettingsField field)
    {
        var panel = NewColumn();
        panel.Margin = new Padding(0, 0, 0, 12);
        var error = NewLabel("", muted: false);
        error.ForeColor = Color.IndianRed;
        error.Visible = false;

        var row = field switch
        {
            SelectField s => CreateSelect(panel, s, error),
            ToggleField t => CreateToggle(panel, t, error),
            IntField i => CreateNumber(panel, i.Id, i.Label, i.Value, i.Min, i.Max, i.Step, i.Unit, isInt: true, error),
            FloatField f => CreateNumber(panel, f.Id, f.Label, f.Value, f.Min, f.Max, f.Step, f.Unit, isInt: false, error),
            StringField s => CreateString(panel, s, error),
            _ => throw new ArgumentException($"Unsupported field type {field.GetType().Name}.", nameof(field)),
        };

        if (field.Description is { Length: > 0 } description) panel.Controls.Add(NewLabel(description, muted: true));
        panel.Controls.Add(error);
        _rows.Add(field.Id, row);
        parent.Controls.Add(panel);
    }

    private FieldRow CreateSelect(Control panel, SelectField field, Label error)
    {
        panel.Controls.Add(NewLabel(field.Label, muted: false));
        var combo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = _width,
            DisplayMember = nameof(SelectOption.Label),
        };
        foreach (var option in field.Options) combo.Items.Add(option);
        panel.Controls.Add(combo);
        _inputs.Add(combo);

        return Wire(
            combo, error, field.Id, field.Value,
            read: () => (combo.SelectedItem as SelectOption)?.Value ?? "",
            show: value => combo.SelectedItem = field.Options.FirstOrDefault(o => o.Value == (string)value),
            changed: handler => combo.SelectedIndexChanged += handler);
    }

    private FieldRow CreateToggle(Control panel, ToggleField field, Label error)
    {
        var box = new CheckBox { Text = field.Label, AutoSize = true, MaximumSize = new Size(_width, 0) };
        panel.Controls.Add(box);

        return Wire(
            box, error, field.Id, field.Value,
            read: () => box.Checked,
            show: value => box.Checked = (bool)value,
            changed: handler => box.CheckedChanged += handler);
    }

    private FieldRow CreateString(Control panel, StringField field, Label error)
    {
        panel.Controls.Add(NewLabel(field.Label, muted: false));
        var box = new TextBox { Width = _width };
        panel.Controls.Add(box);
        _inputs.Add(box);

        return Wire(
            box, error, field.Id, field.Value,
            read: () => box.Text,
            show: value => box.Text = (string)value,
            changed: handler =>
            {
                // Not on every keystroke: the plugin would validate half-typed text.
                box.Leave += handler;
                box.KeyDown += (sender, e) =>
                {
                    if (e.KeyCode != Keys.Enter) return;
                    e.SuppressKeyPress = true;
                    handler(sender, e);
                };
            });
    }

    private FieldRow CreateNumber(
        Control panel, string id, string label, double value, double? min, double? max, double step, string? unit,
        bool isInt, Label error)
    {
        var decimals = isInt ? 0 : Decimals(step);
        object Typed(double number) => isInt ? (object)(int)Math.Round(number) : number;

        if (min is { } low && max is { } high)
        {
            // TrackBar is int-only, so it counts steps from Min and the value is Min + position * Step.
            // Validate guarantees the range is a whole number of steps.
            var positions = (int)Math.Round((high - low) / step);
            double ValueAt(int position) => Math.Round(low + position * step, 6);
            string Readout(double number) => number.ToString("F" + decimals, CultureInfo.CurrentCulture)
                + (string.IsNullOrEmpty(unit) ? "" : " " + unit);

            panel.Controls.Add(NewLabel(label, muted: false));
            var track = new TrackBar
            {
                Minimum = 0,
                Maximum = positions,
                TickStyle = TickStyle.None,
                AutoSize = false,
                Width = _width,
                Height = 32,
            };
            var readout = NewLabel("", muted: false);
            panel.Controls.Add(track);
            panel.Controls.Add(readout);
            _inputs.Add(track);

            return Wire(
                track, error, id, Typed(value),
                read: () => Typed(ValueAt(track.Value)),
                show: shown =>
                {
                    track.Value = Math.Clamp((int)Math.Round((Convert.ToDouble(shown) - low) / step), 0, positions);
                    readout.Text = Readout(ValueAt(track.Value));
                },
                // While the mouse drags the thumb the value changes continuously: the edit is the value where
                // it is let go. Any other change (keys, wheel, code) is a single step and is sent at once.
                changed: handler =>
                {
                    track.ValueChanged += (sender, e) =>
                    {
                        readout.Text = Readout(ValueAt(track.Value));
                        if (MouseButtons == MouseButtons.None) handler(sender, e);
                    };
                    track.MouseUp += (sender, e) => handler(sender, e);
                });
        }

        panel.Controls.Add(NewLabel(string.IsNullOrEmpty(unit) ? label : $"{label} ({unit})", muted: false));
        var box = new NumericUpDown
        {
            Width = 200,
            DecimalPlaces = decimals,
            Increment = (decimal)step,
            Minimum = (decimal)(min ?? (isInt ? int.MinValue : -1e9)),
            Maximum = (decimal)(max ?? (isInt ? int.MaxValue : 1e9)),
        };
        panel.Controls.Add(box);
        _inputs.Add(box);

        return Wire(
            box, error, id, Typed(value),
            read: () => isInt ? (object)(int)box.Value : (double)box.Value,
            show: shown => box.Value = Math.Clamp((decimal)Convert.ToDouble(shown), box.Minimum, box.Maximum),
            changed: handler => box.ValueChanged += handler);
    }

    /// <summary>
    /// Shows the first value, remembers what the control then holds as the last good one (a slider rounds
    /// to its steps), and routes the control's change event to <see cref="CommitAsync"/>.
    /// </summary>
    private FieldRow Wire(
        Control input, Label error, string id, object value, Func<object> read, Action<object> show,
        Action<EventHandler> changed)
    {
        Quietly(() => show(value));
        changed((_, _) =>
        {
            if (!_loading) LastCommit = CommitAsync(id);
        });

        return new FieldRow(input, error, read, show, read());
    }

    private void Quietly(Action action)
    {
        var before = _loading;
        _loading = true;
        try
        {
            action();
        }
        finally
        {
            _loading = before;
        }
    }

    private static FlowLayoutPanel NewColumn() => new()
    {
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
    };

    private Label NewLabel(string text, bool muted)
    {
        var label = new Label
        {
            Text = text,
            AutoSize = true,
            MaximumSize = new Size(_width, 0),
            Margin = new Padding(0, 0, 0, 3),
        };
        if (muted) _muted.Add(label);
        return label;
    }

    /// <summary>How many decimals a step needs, up to six.</summary>
    private static int Decimals(double step)
    {
        for (var places = 0; places < 6; places++)
        {
            var scaled = step * Math.Pow(10, places);
            if (Math.Abs(scaled - Math.Round(scaled)) < 1e-9) return places;
        }

        return 6;
    }
}
