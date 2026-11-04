using Grpc.Core;
using Ocrx.Contracts.Proto;
using Xunit;

namespace Ocrx.Sdk.Tests;

/// <summary>
/// The "open your settings window" contract: the Hello flag, the engine's OpenSettings arm reaching
/// the plugin without stalling ticks, and the old-peer cases that must behave as before.
/// </summary>
public class OpenSettingsTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private static TrackSession Session(RecordingClientStreamWriter<TrackRequest> writer,
        params TrackResponse[] responses)
    {
        var call = new AsyncDuplexStreamingCall<TrackRequest, TrackResponse>(
            writer,
            new QueueAsyncStreamReader<TrackResponse>(responses),
            Task.FromResult(new Metadata()),
            () => new Status(StatusCode.OK, string.Empty),
            () => new Metadata(),
            () => { });
        return new TrackSession(call);
    }

    private static TickDispatcher Dispatcher(StubPlugin plugin, out RecordingOutput output)
    {
        output = new RecordingOutput();
        return new TickDispatcher(plugin, new PluginServices([], output, verbose: true, dumpFrame: null), output);
    }

    [Fact]
    public void WireIdentity_IsHelloFieldThree_AndTrackResponseArmFive()
    {
        Assert.Equal(3, Hello.HasSettingsWindowFieldNumber);
        Assert.Equal(5, TrackResponse.OpenSettingsFieldNumber);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Hello_CarriesTheDeclaredFlag(bool declared)
    {
        var writer = new RecordingClientStreamWriter<TrackRequest>();
        await using var session = Session(writer);

        await session.SendHelloAsync("stub", 1, declared);

        Assert.Equal(declared, Assert.Single(writer.Messages).Hello.HasSettingsWindow);
    }

    [Fact]
    public async Task Hello_WithoutTheFlagArgument_DeclaresNoSettingsWindow()
    {
        var writer = new RecordingClientStreamWriter<TrackRequest>();
        await using var session = Session(writer);

        await session.SendHelloAsync("stub", 1);

        Assert.False(Assert.Single(writer.Messages).Hello.HasSettingsWindow);
    }

    [Fact]
    public void ADefaultPlugin_HasNoSettingsWindow_AndOnOpenSettingsIsANoOp()
    {
        IOcrxPlugin plugin = new BareboneStubPlugin();

        Assert.False(plugin.HasSettingsWindow);
        Assert.True(plugin.OnOpenSettings(new PluginServices([], new RecordingOutput(), true, null),
            default).IsCompletedSuccessfully);
    }

    [Fact]
    public async Task OpenSettings_ReachesTheHandler_WithoutYieldingATick()
    {
        var writer = new RecordingClientStreamWriter<TrackRequest>();
        await using var session = Session(writer,
            new TrackResponse { OpenSettings = new OpenSettings() },
            new TrackResponse { Tick = new TickResult { TimestampMs = 1, FrameSeq = 3 } });
        var opened = 0;
        session.OpenSettingsHandler = _ => opened++;

        var ticks = new List<TickData>();
        await foreach (var tick in session.Ticks(CancellationToken.None))
            ticks.Add(tick);

        Assert.Equal(1, opened);
        Assert.Equal(3ul, Assert.Single(ticks).FrameSeq);
    }

    [Fact]
    public async Task OpenSettings_WithNoHandlerWired_IsDropped()
    {
        var writer = new RecordingClientStreamWriter<TrackRequest>();
        await using var session = Session(writer,
            new TrackResponse { OpenSettings = new OpenSettings() },
            new TrackResponse { Tick = new TickResult { TimestampMs = 1, FrameSeq = 3 } });

        var ticks = new List<TickData>();
        await foreach (var tick in session.Ticks(CancellationToken.None))
            ticks.Add(tick);

        Assert.Single(ticks);
    }

    [Fact]
    public async Task AnEngineThatNeverSendsOpenSettings_DeliversTicksAsBefore()
    {
        var writer = new RecordingClientStreamWriter<TrackRequest>();
        await using var session = Session(writer,
            new TrackResponse { Tick = new TickResult { TimestampMs = 1, FrameSeq = 1 } },
            new TrackResponse { Tick = new TickResult { TimestampMs = 2, FrameSeq = 2 } });
        var opened = 0;
        session.OpenSettingsHandler = _ => opened++;

        var count = 0;
        await foreach (var _ in session.Ticks(CancellationToken.None))
            count++;

        Assert.Equal(2, count);
        Assert.Equal(0, opened);
    }

    [Fact]
    public async Task Dispatcher_InvokesOnOpenSettings_WithTheServices()
    {
        var seen = new TaskCompletionSource<IPluginServices>(TaskCreationOptions.RunContinuationsAsynchronously);
        var plugin = new StubPlugin(onOpenSettings: (services, _) =>
        {
            seen.SetResult(services);
            return Task.CompletedTask;
        });
        var dispatcher = Dispatcher(plugin, out _);

        dispatcher.OpenSettings(default);

        Assert.NotNull(await seen.Task.WaitAsync(Patience));
    }

    [Fact]
    public async Task Dispatcher_DoesNotBlockTheCaller_WhileTheWindowOpens()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var plugin = new StubPlugin(onOpenSettings: async (_, _) =>
        {
            entered.SetResult();
            await release.Task;
        });
        var dispatcher = Dispatcher(plugin, out _);

        dispatcher.OpenSettings(default);
        await entered.Task.WaitAsync(Patience);

        // The plugin is still inside OnOpenSettings, yet the tick loop's next dispatch completes.
        await dispatcher.DispatchAsync(TickFactory.Tick(1), default).WaitAsync(Patience);
        Assert.Single(plugin.Ticks);

        release.SetResult();
    }

    [Fact]
    public async Task Dispatcher_DoesNotBlockTheCaller_WhenThePluginBlocksBeforeItsFirstAwait()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var plugin = new StubPlugin(onOpenSettings: (_, _) =>
        {
            entered.SetResult();
            gate.Task.Wait();   // synchronous: no await to yield on, so only Task.Run keeps the caller free
            return Task.CompletedTask;
        });
        var dispatcher = Dispatcher(plugin, out _);

        dispatcher.OpenSettings(default);
        await entered.Task.WaitAsync(Patience);

        await dispatcher.DispatchAsync(TickFactory.Tick(1), default).WaitAsync(Patience);
        Assert.Single(plugin.Ticks);

        gate.SetResult();
    }

    [Fact]
    public async Task Dispatcher_LogsAThrowingOnOpenSettings_AndCarriesOn()
    {
        var logged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var plugin = new StubPlugin(onOpenSettings: (_, _) => throw new InvalidOperationException("no window"));
        var output = new SignalOutput(logged);
        var dispatcher = new TickDispatcher(plugin,
            new PluginServices([], output, verbose: true, dumpFrame: null), output);

        dispatcher.OpenSettings(default);
        await logged.Task.WaitAsync(Patience);

        Assert.Contains("opening settings failed: no window", output.Last);
        await dispatcher.DispatchAsync(TickFactory.Tick(1), default);
        Assert.Single(plugin.Ticks);
    }
}
