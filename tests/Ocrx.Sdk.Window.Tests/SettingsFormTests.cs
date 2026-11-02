using System.Drawing;
using System.Windows.Forms;

namespace Ocrx.Sdk.Window.Tests;

public sealed class SettingsFormTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(15);

    private static readonly SelectOption A = new("a", "Alpha");
    private static readonly SelectOption B = new("b", "Beta");

    private static IReadOnlyList<SettingsField> Sample() =>
    [
        new SelectField("sel", "Choice", "a", [A, B]),
        new ToggleField("tog", "Enabled", false),
        new IntField("slideInt", "Scale", 50, Min: 0, Max: 100, Step: 5, Unit: "%"),
        new IntField("boxInt", "Count", 3),
        new FloatField("slideFloat", "Opacity", 0.5, Min: 0, Max: 2, Step: 0.25),
        new FloatField("boxFloat", "Gain", 1.5, Step: 0.25),
        new StringField("str", "Name", "hello", Description: "Shown on the overlay."),
        new GroupField("grp", "Advanced", [new StringField("inner", "Inner", "x")]),
        new HelpField("help", "Some help text."),
    ];

    private static IEnumerable<T> All<T>(Control root) where T : Control
    {
        foreach (Control child in root.Controls)
        {
            if (child is T match) yield return match;
            foreach (var nested in All<T>(child)) yield return nested;
        }
    }

    [Fact]
    public async Task Fields_MapToTheExpectedControls()
    {
        using var rig = new SettingsFormRig(Sample());
        await rig.ShowAsync();

        Assert.Equal("a", Assert.IsType<SelectOption>(rig.Input<ComboBox>("sel").SelectedItem).Value);
        Assert.False(rig.Input<CheckBox>("tog").Checked);
        Assert.IsType<TrackBar>(rig.Input<TrackBar>("slideInt"));
        Assert.IsType<NumericUpDown>(rig.Input<NumericUpDown>("boxInt"));
        Assert.IsType<TrackBar>(rig.Input<TrackBar>("slideFloat"));
        Assert.IsType<NumericUpDown>(rig.Input<NumericUpDown>("boxFloat"));
        Assert.Equal("hello", rig.Input<TextBox>("str").Text);
        Assert.Equal("x", rig.Input<TextBox>("inner").Text);

        var texts = rig.Ui(form => All<Label>(form).Select(l => l.Text).ToArray());
        Assert.Contains("Some help text.", texts);
        Assert.Contains("Shown on the overlay.", texts);
        Assert.Equal(1, rig.Ui(form => All<GroupBox>(form).Count()));
    }

    [Fact]
    public async Task BoundedNumbers_AreSlidersWithAReadout()
    {
        using var rig = new SettingsFormRig(Sample());
        await rig.ShowAsync();

        var texts = rig.Ui(form => All<Label>(form).Select(l => l.Text).ToArray());
        Assert.Contains("50 %", texts);
        Assert.Contains("0.50", texts);
        Assert.Equal(10, rig.Input<TrackBar>("slideInt").Value);
        Assert.Equal(2, rig.Input<TrackBar>("slideFloat").Value);
        Assert.Equal(0, rig.Input<NumericUpDown>("boxInt").DecimalPlaces);
        Assert.Equal(2, rig.Input<NumericUpDown>("boxFloat").DecimalPlaces);
        Assert.Equal(1.5m, rig.Input<NumericUpDown>("boxFloat").Value);
    }

    [Fact]
    public async Task Commit_SendsTheTypedValue()
    {
        using var rig = new SettingsFormRig(Sample());
        await rig.ShowAsync();

        await rig.EditAsync("sel", form => ((ComboBox)form.InputOf("sel")).SelectedIndex = 1);
        await rig.EditAsync("tog", form => ((CheckBox)form.InputOf("tog")).Checked = true);
        await rig.EditAsync("slideInt", form => ((TrackBar)form.InputOf("slideInt")).Value = 4);
        await rig.EditAsync("boxInt", form => ((NumericUpDown)form.InputOf("boxInt")).Value = 7);
        await rig.EditAsync("slideFloat", form => ((TrackBar)form.InputOf("slideFloat")).Value = 3);
        await rig.EditAsync("boxFloat", form => ((NumericUpDown)form.InputOf("boxFloat")).Value = 2.25m);
        await rig.EditAsync("str", form => ((TextBox)form.InputOf("str")).Text = "world");

        Assert.Equal(
            [
                new SettingsEdit("sel", "b"),
                new SettingsEdit("tog", true),
                new SettingsEdit("slideInt", 20),
                new SettingsEdit("boxInt", 7),
                new SettingsEdit("slideFloat", 0.75),
                new SettingsEdit("boxFloat", 2.25),
                new SettingsEdit("str", "world"),
            ],
            rig.Edits);
    }

    [Fact]
    public async Task Commit_UnchangedValue_SendsNothing()
    {
        using var rig = new SettingsFormRig(Sample());
        await rig.ShowAsync();

        await rig.CommitAsync("str");
        await rig.CommitAsync("slideInt");

        Assert.Empty(rig.Edits);
    }

    [Fact]
    public async Task Refresh_ShowsTheReturnedFields()
    {
        IReadOnlyList<SettingsField> next = [new StringField("str", "Name", "from plugin"), new ToggleField("tog", "Enabled", true)];
        using var rig = new SettingsFormRig(Sample(), _ => Task.FromResult(SettingsResult.Refresh(next)));
        await rig.ShowAsync();

        await rig.EditAsync("str", form => ((TextBox)form.InputOf("str")).Text = "typed");

        Assert.Equal("from plugin", rig.Input<TextBox>("str").Text);
        Assert.True(rig.Input<CheckBox>("tog").Checked);
        Assert.Throws<KeyNotFoundException>(() => rig.Input<ComboBox>("sel"));
    }

    [Fact]
    public async Task Fail_RevertsTheFieldAndShowsTheError_UntilTheNextSuccess()
    {
        var answers = new Queue<bool>([false, true]);
        using var rig = new SettingsFormRig(
            Sample(),
            _ => Task.FromResult(answers.Dequeue() ? SettingsResult.Refresh(Sample()) : SettingsResult.Fail("Not allowed.")));
        await rig.ShowAsync();

        await rig.EditAsync("str", form => ((TextBox)form.InputOf("str")).Text = "bad");

        Assert.Equal("hello", rig.Input<TextBox>("str").Text);
        Assert.Equal("Not allowed.", rig.Ui(form => form.ErrorOf("str")));

        // The next edit that succeeds clears the message.
        await rig.EditAsync("tog", form => ((CheckBox)form.InputOf("tog")).Checked = true);
        Assert.Null(rig.Ui(form => form.ErrorOf("str")));
    }

    [Fact]
    public async Task CallbackThrows_ShowsTheMessageAndReverts()
    {
        using var rig = new SettingsFormRig(Sample(), _ => throw new InvalidOperationException("boom"));
        await rig.ShowAsync();

        await rig.EditAsync("tog", form => ((CheckBox)form.InputOf("tog")).Checked = true);

        Assert.False(rig.Input<CheckBox>("tog").Checked);
        Assert.Equal("boom", rig.Ui(form => form.ErrorOf("tog")));
    }

    [Fact]
    public async Task Edits_AreHandledOneAtATime_AndAWaitingOneIsSentAfter()
    {
        var gate = new TaskCompletionSource<SettingsResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = 0;
        var overlapped = false;
        var calls = 0;
        using var rig = new SettingsFormRig(Sample(), async _ =>
        {
            if (Interlocked.Increment(ref running) > 1) overlapped = true;
            try
            {
                return Interlocked.Increment(ref calls) == 1 ? await gate.Task : SettingsResult.Refresh(Sample());
            }
            finally
            {
                Interlocked.Decrement(ref running);
            }
        });
        await rig.ShowAsync();

        rig.Ui(form => { ((CheckBox)form.InputOf("tog")).Checked = true; return 0; });
        Assert.Single(rig.Edits);
        Assert.True(rig.Input<TextBox>("str").Enabled); // input is never disabled: the user keeps their focus

        rig.Ui(form => { ((TextBox)form.InputOf("str")).Text = "second"; return 0; });
        await rig.CommitAsync("str"); // returns at once: an edit is running
        Assert.Single(rig.Edits);

        gate.SetResult(SettingsResult.Fail("first refused")); // no rebuild, so the typed text survives
        await rig.Ui(form => form.LastCommit).WaitAsync(Limit);

        Assert.Equal([new SettingsEdit("tog", true), new SettingsEdit("str", "second")], rig.Edits);
        Assert.False(overlapped);
    }

    [Fact]
    public async Task WaitingEdit_SurvivesARefreshThatRebuiltTheForm()
    {
        var gate = new TaskCompletionSource<SettingsResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        IReadOnlyList<SettingsField> accepted = [new StringField("str", "Name", "second"), new ToggleField("tog", "Enabled", true)];
        using var rig = new SettingsFormRig(
            Sample(), async _ => Interlocked.Increment(ref calls) == 1 ? await gate.Task : SettingsResult.Refresh(accepted));
        await rig.ShowAsync();

        rig.Ui(form => { ((CheckBox)form.InputOf("tog")).Checked = true; return 0; });
        rig.Ui(form => { ((TextBox)form.InputOf("str")).Text = "second"; return 0; });
        await rig.CommitAsync("str"); // waits behind the toggle

        // The plugin answers the toggle with its own idea of the form, where "str" is still "hello".
        gate.SetResult(SettingsResult.Refresh(Sample()));
        await rig.Ui(form => form.LastCommit).WaitAsync(Limit);

        Assert.Equal([new SettingsEdit("tog", true), new SettingsEdit("str", "second")], rig.Edits);
        Assert.Equal("second", rig.Input<TextBox>("str").Text);
    }

    [Fact]
    public async Task Refresh_FromACallbackThatFinishesAtOnce_DoesNotRebuildInsideTheControlsEvent()
    {
        IReadOnlyList<SettingsField> After(string text) => [new SelectField("sel", "Choice", "a", [A, B]),
            new ToggleField("tog", "Enabled", true), new StringField("str", "Name", text)];
        var answers = new Queue<string>(["from toggle", "from combo"]);
        using var rig = new SettingsFormRig(
            Sample(), _ => Task.FromResult(SettingsResult.Refresh(After(answers.Dequeue()))));
        await rig.ShowAsync();

        await rig.EditAsync("tog", form => ((CheckBox)form.InputOf("tog")).Checked = true);
        Assert.Equal("from toggle", rig.Input<TextBox>("str").Text);

        await rig.EditAsync("sel", form => ((ComboBox)form.InputOf("sel")).SelectedIndex = 1);
        Assert.Equal("from combo", rig.Input<TextBox>("str").Text);
    }

    [Fact]
    public async Task Refresh_KeepsFocusOnTheFieldTheUserIsIn()
    {
        using var rig = new SettingsFormRig(Sample());
        await rig.ShowAsync();
        rig.Ui(form => { form.ActiveControl = form.InputOf("str"); return 0; });

        await rig.EditAsync("tog", form => ((CheckBox)form.InputOf("tog")).Checked = true);

        Assert.True(rig.Ui(form => form.ActiveControl == form.InputOf("str")));
    }

    [Fact]
    public async Task Refresh_KeepsTheScrollPosition()
    {
        IReadOnlyList<SettingsField> many = [.. Enumerable.Range(0, 40).Select(i => new StringField($"f{i}", $"Field {i}", ""))];
        using var rig = new SettingsFormRig(many);
        await rig.ShowAsync();
        rig.Ui(form =>
        {
            form.AutoScrollPosition = new Point(0, 300);
            // Focus a field that is in view: the window being activated late would otherwise focus the first
            // one and scroll back to it, which is not what this test is about.
            var inView = many.Select(f => form.InputOf(f.Id))
                .First(c => form.ClientRectangle.Contains(form.RectangleToClient(c.RectangleToScreen(c.ClientRectangle))));
            form.ActiveControl = inView;
            return 0;
        });
        var before = rig.Ui(form => -form.AutoScrollPosition.Y);
        Assert.True(before > 0);

        await rig.EditAsync("f1", form => ((TextBox)form.InputOf("f1")).Text = "x");

        Assert.Equal(before, rig.Ui(form => -form.AutoScrollPosition.Y));
    }

    [Fact]
    public async Task Colours_ReachTheInputsThatDoNotInheritThem()
    {
        using var rig = new SettingsFormRig(Sample());
        await rig.ShowAsync();

        rig.Ui(form =>
        {
            form.BackColor = Color.Black;
            form.ForeColor = Color.White;
            return 0;
        });

        foreach (var id in new[] { "str", "sel", "boxInt", "slideInt" })
            Assert.Equal(Color.Black, rig.Ui(form => form.InputOf(id).BackColor));

        // A TrackBar has no text to colour.
        foreach (var id in new[] { "str", "sel", "boxInt" })
            Assert.Equal(Color.White, rig.Ui(form => form.InputOf(id).ForeColor));
    }

    [Theory]
    [MemberData(nameof(Undrawable))]
    public void Form_UndrawableFields_Throw(IReadOnlyList<SettingsField> fields)
    {
        static Task<SettingsResult> Ignore(SettingsEdit edit) => Task.FromResult(SettingsResult.Fail("x"));

        Assert.ThrowsAny<ArgumentException>(() => new SettingsForm("t", () => fields, Ignore));
    }

    public static TheoryData<IReadOnlyList<SettingsField>> Undrawable() => new()
    {
        { [new GroupField("g", "G", [new GroupField("h", "H", [])])] },
        { [new StringField("a", "A", ""), new StringField("a", "B", "")] },
        { [new IntField("i", "I", 1, Step: 0)] },
        { [new IntField("i", "I", 1, Min: 5, Max: 5)] },
        { [new IntField("i", "I", 1, Min: 0, Max: 100, Step: 60)] },
        { [new FloatField("f", "F", 1, Min: 0, Max: 1, Step: 0.3)] },
    };

    [Fact]
    public void Form_ARangeThatIsWholeStepsIsAccepted()
    {
        using var form = new SettingsForm(
            "t", () => [new FloatField("f", "F", 0.5, Min: 0, Max: 1, Step: 0.1)], _ => Task.FromResult(SettingsResult.Fail("x")));

        Assert.IsType<TrackBar>(form.InputOf("f"));
    }

    [Fact]
    public async Task Slider_ValueChange_CommitsWithoutAMouseUp_AndMovesTheReadout()
    {
        var gate = new TaskCompletionSource<SettingsResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var rig = new SettingsFormRig(Sample(), _ => gate.Task);
        await rig.ShowAsync();

        // The mouse wheel and the arrow keys change the value with no button down, as does code.
        rig.Ui(form => { ((TrackBar)form.InputOf("slideInt")).Value = 4; return 0; });

        Assert.Equal([new SettingsEdit("slideInt", 20)], rig.Edits);
        Assert.Contains("20 %", rig.Ui(form => All<Label>(form).Select(l => l.Text).ToArray()));
        gate.SetResult(SettingsResult.Fail("done"));
    }

    [Fact]
    public async Task Refresh_KeepsTextTypedInAnotherFieldAndSendsIt()
    {
        IReadOnlyList<SettingsField> Answer(SettingsEdit edit) => edit.FieldId == "str"
            ? [new StringField("str", "Name", (string)edit.Value), new ToggleField("tog", "Enabled", true)]
            : Sample();
        using var rig = new SettingsFormRig(Sample(), edit => Task.FromResult(SettingsResult.Refresh(Answer(edit))));
        await rig.ShowAsync();
        rig.Ui(form => { ((TextBox)form.InputOf("str")).Text = "typed, not left"; return 0; });

        await rig.EditAsync("tog", form => ((CheckBox)form.InputOf("tog")).Checked = true);
        await rig.Ui(form => form.LastCommit).WaitAsync(Limit);

        Assert.Equal("typed, not left", rig.Input<TextBox>("str").Text);
        Assert.Equal([new SettingsEdit("tog", true), new SettingsEdit("str", "typed, not left")], rig.Edits);
    }

    [Fact]
    public async Task Refresh_OfAnUndrawableForm_KeepsTheOldControlsAndFailsTheEdit()
    {
        IReadOnlyList<SettingsField> bad = [new StringField("x", "X", ""), new StringField("x", "Y", "")];
        using var rig = new SettingsFormRig(Sample(), _ => Task.FromResult(SettingsResult.Refresh(bad)));
        await rig.ShowAsync();
        var before = rig.Input<CheckBox>("tog");

        await rig.EditAsync("tog", form => ((CheckBox)form.InputOf("tog")).Checked = true);

        Assert.Same(before, rig.Input<CheckBox>("tog"));
        Assert.False(before.Checked);
        Assert.Contains("Duplicate field id x", rig.Ui(form => form.ErrorOf("tog")));
    }

    [Fact]
    public async Task CommitDirty_SendsWhatWasTypedButNeverLeft()
    {
        using var rig = new SettingsFormRig(Sample());
        await rig.ShowAsync();
        rig.Ui(form => { ((TextBox)form.InputOf("str")).Text = "pending"; return 0; });
        Assert.Empty(rig.Edits);

        await rig.CommitDirtyAsync();

        Assert.Equal([new SettingsEdit("str", "pending")], rig.Edits);
    }

    [Fact]
    public async Task HideClose_SendsWhatWasTyped()
    {
        using var rig = new SettingsFormRig(Sample());
        await rig.ShowAsync();
        rig.Ui(form => { ((TextBox)form.InputOf("str")).Text = "before closing"; return 0; });

        rig.Ui(form => { form.Close(); return 0; });
        await rig.Ui(form => form.LastCommit).WaitAsync(Limit);

        Assert.Equal([new SettingsEdit("str", "before closing")], rig.Edits);
    }

    [Fact]
    public async Task DisposingTheHost_DoesNotSendWhatWasTyped()
    {
        var rig = new SettingsFormRig(Sample());
        await rig.ShowAsync();
        rig.Ui(form => { ((TextBox)form.InputOf("str")).Text = "never sent"; return 0; });

        rig.Dispose(); // closes the form for real

        Assert.Empty(rig.Edits);
    }

    [Fact]
    public async Task ShowingAgain_ReadsTheFieldsAgain()
    {
        var reads = 0;
        using var rig = new SettingsFormRig(
            () => [new StringField("str", "Name", "value " + Interlocked.Increment(ref reads))]);
        await rig.ShowAsync();
        Assert.Equal("value 1", rig.Input<TextBox>("str").Text);

        rig.Ui(form => { form.Close(); return 0; }); // hides
        await rig.ShowAsync();

        Assert.Equal("value 2", rig.Input<TextBox>("str").Text);
    }

    [Fact]
    public async Task Group_DoesNotWidenTheFormPastItsClientArea()
    {
        IReadOnlyList<SettingsField> fields =
        [
            new GroupField("g", "Advanced", [new StringField("a", "A", ""), new SelectField("b", "B", "a", [A, B])]),
            .. Enumerable.Range(0, 30).Select(i => new StringField($"f{i}", $"Field {i}", "")),
        ];
        using var rig = new SettingsFormRig(fields);
        await rig.ShowAsync();

        Assert.True(rig.Ui(form => form.VerticalScroll.Visible));
        Assert.False(rig.Ui(form => form.HorizontalScroll.Visible));
    }

    [Fact]
    public void Result_CannotBeBuiltInvalid()
    {
        Assert.Throws<ArgumentException>(() => SettingsResult.Fail(" "));
        Assert.Throws<ArgumentNullException>(() => SettingsResult.Refresh(null!));
        Assert.Equal("no", SettingsResult.Fail("no").Error);
        Assert.Null(SettingsResult.Refresh([]).Error);
    }
}
