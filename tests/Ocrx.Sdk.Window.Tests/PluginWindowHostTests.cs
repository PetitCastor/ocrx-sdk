using System.Drawing;
using System.Windows.Forms;

namespace Ocrx.Sdk.Window.Tests;

public sealed class PluginWindowHostTests
{
    // A hung STA thread must fail the test, not the suite.
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task ShowAsync_BuildsFormLazilyOnAnStaThread()
    {
        using var rig = new HostRig();
        Assert.Equal(0, rig.Created);

        await rig.Host.ShowAsync().WaitAsync(Limit);

        Assert.Equal(1, rig.Created);
        Assert.NotSame(Thread.CurrentThread, rig.FormThread);
        Assert.Equal(ApartmentState.STA, rig.FormThread!.GetApartmentState());
        Assert.True(rig.OnForm(f => f.Visible));
        Assert.Equal(1, rig.Platform.ForegroundCalls);
    }

    [Fact]
    public async Task ShowAsync_Twice_ReusesTheForm()
    {
        using var rig = new HostRig();

        await rig.Host.ShowAsync().WaitAsync(Limit);
        var first = rig.Form;
        await rig.Host.ShowAsync().WaitAsync(Limit);

        Assert.Equal(1, rig.Created);
        Assert.Same(first, rig.Form);
        Assert.Equal(2, rig.Platform.ForegroundCalls);
    }

    [Fact]
    public async Task ShowAsync_MinimisedForm_IsRestored()
    {
        using var rig = new HostRig();
        await rig.Host.ShowAsync().WaitAsync(Limit);
        rig.OnForm(f => { f.WindowState = FormWindowState.Minimized; return 0; });

        await rig.Host.ShowAsync().WaitAsync(Limit);

        Assert.Equal(FormWindowState.Normal, rig.OnForm(f => f.WindowState));
    }

    [Fact]
    public async Task UserClose_HidesTheFormAndKeepsIt()
    {
        using var rig = new HostRig();
        await rig.Host.ShowAsync().WaitAsync(Limit);

        rig.OnForm(f => { f.Close(); return 0; });

        Assert.False(rig.OnForm(f => f.Visible));
        Assert.False(rig.OnForm(f => f.IsDisposed));

        await rig.Host.ShowAsync().WaitAsync(Limit);
        Assert.Equal(1, rig.Created);
        Assert.True(rig.OnForm(f => f.Visible));
    }

    [Fact]
    public async Task DisposedForm_IsRebuiltOnNextShow()
    {
        using var rig = new HostRig();
        await rig.Host.ShowAsync().WaitAsync(Limit);
        var first = rig.Form;
        rig.OnForm(f => { f.Dispose(); return 0; });

        await rig.Host.ShowAsync().WaitAsync(Limit);

        Assert.Equal(2, rig.Created);
        Assert.NotSame(first, rig.Form);
    }

    [Fact]
    public async Task Dispose_ClosesTheFormAndEndsTheThread()
    {
        var rig = new HostRig();
        await rig.Host.ShowAsync().WaitAsync(Limit);
        var form = rig.Form!;
        var thread = rig.FormThread!;

        rig.Dispose();

        Assert.False(thread.IsAlive);
        Assert.True(form.IsDisposed);
        rig.Dispose(); // idempotent
    }

    [Fact]
    public async Task Dispose_BeforeAnyShow_StartsNothing()
    {
        var rig = new HostRig();

        rig.Dispose();
        await rig.Host.ShowAsync().WaitAsync(Limit);

        Assert.Equal(0, rig.Created);
    }

    [Fact]
    public async Task Dispose_EndsTheThreadEvenWhenClosingTheFormThrows()
    {
        var rig = new HostRig(() =>
        {
            var form = new Form { ShowInTaskbar = false };
            form.FormClosing += (_, _) => throw new InvalidOperationException("will not close");
            return form;
        });
        await rig.Host.ShowAsync().WaitAsync(Limit);
        var thread = rig.FormThread!;

        rig.Dispose();

        Assert.False(thread.IsAlive);
    }

    [Fact]
    public async Task ShowAsync_AfterDispose_DoesNothing()
    {
        var rig = new HostRig();
        await rig.Host.ShowAsync().WaitAsync(Limit);
        rig.Dispose();

        await rig.Host.ShowAsync().WaitAsync(Limit);

        Assert.Equal(1, rig.Created);
    }

    [Fact]
    public async Task ShowAsync_FactoryThrows_FaultsTheTaskAndHostStillDisposes()
    {
        using var rig = new HostRig(() => throw new InvalidOperationException("no form"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Host.ShowAsync().WaitAsync(Limit));

        Assert.Equal("no form", ex.Message);
    }

    [Fact]
    public async Task Theme_AppliesToTheOpenFormAndTitleBar()
    {
        using var rig = new HostRig();
        await rig.Host.ShowAsync().WaitAsync(Limit);

        rig.Host.Theme = WindowTheme.Dark;
        // The setter posts to the UI thread. ShowAsync posts too, so its completion means the theme ran
        // (Form.Invoke would not: a sent message overtakes a posted one).
        await rig.Host.ShowAsync().WaitAsync(Limit);
        Assert.Equal(ThemeColors.Back(true), rig.OnForm(f => f.BackColor));
        Assert.True(rig.Platform.TitleBars[^1]);

        rig.Host.Theme = WindowTheme.Light;
        await rig.Host.ShowAsync().WaitAsync(Limit);
        Assert.Equal(ThemeColors.Back(false), rig.OnForm(f => f.BackColor));
        Assert.False(rig.Platform.TitleBars[^1]);
        Assert.Equal(WindowTheme.Light, rig.Host.Theme);
    }

    [Fact]
    public async Task Theme_SetBeforeShow_AppliesWhenTheFormIsBuilt()
    {
        using var rig = new HostRig();
        rig.Host.Theme = WindowTheme.Dark;

        await rig.Host.ShowAsync().WaitAsync(Limit);

        Assert.Equal(ThemeColors.Back(true), rig.OnForm(f => f.BackColor));
    }

    [Fact]
    public async Task Theme_System_FollowsThePlatform()
    {
        using var rig = new HostRig();
        rig.Platform.SystemDark = true;

        await rig.Host.ShowAsync().WaitAsync(Limit);

        Assert.Equal(WindowTheme.System, rig.Host.Theme);
        Assert.Equal(ThemeColors.Back(true), rig.OnForm(f => f.BackColor));
    }

    [Fact]
    public async Task Icon_IsGivenToTheForm()
    {
        using var rig = new HostRig();
        rig.Host.Icon = SystemIcons.Information;

        await rig.Host.ShowAsync().WaitAsync(Limit);

        Assert.Same(SystemIcons.Information, rig.OnForm(f => f.Icon));
    }
}
