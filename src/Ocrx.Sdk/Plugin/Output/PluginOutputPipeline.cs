using System.Threading.Channels;
using Ocrx.Contracts;

namespace Ocrx.Sdk;

/// <summary>
/// Composes a run's record sinks and delivers queued records to them in order, off the tick thread.
/// </summary>
internal sealed class PluginOutputPipeline
{
    private readonly IPluginOutput _output;

    // Guards the composed sink: the drain reads it around each emit, and a live rebuild
    // (RebuildOutputsAsync, when a settings edit changes an overlay) swaps it. Async work runs
    // under it, so a SemaphoreSlim rather than a lock.
    private readonly SemaphoreSlim _sinkGate = new(1, 1);
    private IRecordSink _sink;

    private readonly Channel<CaptureRecord> _outbox =
        Channel.CreateUnbounded<CaptureRecord>(new UnboundedChannelOptions { SingleReader = true });

    private Task? _drain;

    public PluginOutputPipeline(IRecordSink sink, IPluginOutput output)
    {
        _sink = sink;
        _output = output;
    }

    /// <summary>
    /// Builds the configured sinks in declaration order. If a later spec is invalid, every sink
    /// built before it is disposed before the configuration error is returned to the host.
    /// </summary>
    public static async Task<PluginOutputPipeline> CreateAsync(PluginHostOptions options,
        PluginConfig config, Func<bool> isReplay, IPluginOutput output)
        => new(await ComposeAsync(options, config, isReplay, output), output);

    /// <summary>
    /// Composes the sinks for a config into one ordered <see cref="CompositeRecordSink"/> — the
    /// same wiring the run starts with and a live rebuild reproduces. Explicit
    /// <see cref="PluginHostOptions.Sinks"/> win over config; the legacy
    /// <see cref="PluginHostOptions.RecordSink"/> is appended as one more ordered sink. On a bad
    /// spec, everything built before it is disposed before the error propagates.
    /// </summary>
    public static async Task<IRecordSink> ComposeAsync(PluginHostOptions options,
        PluginConfig config, Func<bool> isReplay, IPluginOutput output)
    {
        List<IRecordSink> sinks = [];
        try
        {
            // Explicit options win over config: tests and embedding hosts must not accidentally
            // construct a config-driven sink that writes to a real path.
            if (options.Sinks is { } explicitSinks)
                sinks.AddRange(explicitSinks);
            else
                foreach (var spec in config.Outputs)
                    sinks.Add(SinkFactory.Build(spec, isReplay, output, options.OverlayFactory));
        }
        catch (ArgumentException)
        {
            // A spec past the bad one never got built, but everything before it may own resources.
            foreach (var built in sinks)
                await built.DisposeAsync();
            throw;
        }

        // Keep the legacy callback as one more ordered sink in the same asynchronous pipeline.
        if (options.RecordSink is { } legacy)
            sinks.Add(new DelegateRecordSink(legacy));

        return new CompositeRecordSink(sinks);
    }

    public void Enqueue(CaptureRecord record) => _outbox.Writer.TryWrite(record);

    /// <summary>Starts the run's single background sink-drain task.</summary>
    public void Start(CancellationToken ct) => _drain = Task.Run(() => DrainAsync(ct));

    /// <summary>
    /// Swaps in a freshly composed sink and disposes the one it replaces. The new sink is live
    /// before the old is torn down, so a record enqueued during the swap is never dropped — at
    /// worst it waits behind the swap on the same gate the drain uses.
    /// </summary>
    public async Task ReplaceSinkAsync(IRecordSink newSink)
    {
        ArgumentNullException.ThrowIfNull(newSink);

        IRecordSink old;
        await _sinkGate.WaitAsync();
        try
        {
            old = _sink;
            _sink = newSink;
        }
        finally
        {
            _sinkGate.Release();
        }

        // Dispose outside the gate: tearing an overlay window down must not block emits to the sink
        // that has already taken its place.
        await old.DisposeAsync();
    }

    /// <summary>Flushes every queued record before disposing the composed sink.</summary>
    public async Task CompleteAndDrainAsync()
    {
        _outbox.Writer.TryComplete();
        if (_drain is not null)
            await _drain;

        // The drain has stopped, so nothing else touches _sink; no gate needed here.
        await _sink.DisposeAsync();
    }

    private async Task DrainAsync(CancellationToken ct)
    {
        // CancellationToken.None here, deliberately: a cancelled run still drains what is already
        // queued. The ct passed to EmitAsync is what lets a sink abort a slow write.
        await foreach (var record in _outbox.Reader.ReadAllAsync(CancellationToken.None))
        {
            // Held across the emit so a rebuild cannot dispose the sink mid-write; the swap simply
            // waits for the in-flight emit to finish.
            await _sinkGate.WaitAsync(CancellationToken.None);
            try { await _sink.EmitAsync(record, ct); }
            catch (Exception ex) { _output.WriteLine($"sink error: {ex.Message}"); }
            finally { _sinkGate.Release(); }
        }
    }
}
