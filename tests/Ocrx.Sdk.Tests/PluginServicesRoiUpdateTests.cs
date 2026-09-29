using Grpc.Core;
using Ocrx.Contracts;
using Ocrx.Contracts.Proto;
using Xunit;

namespace Ocrx.Sdk.Tests;

/// <summary>
/// <see cref="IPluginServices.UpdateRoisAsync"/>: a plugin whose regions move at runtime replaces its
/// whole set on the live session, and a call with no session is a quiet no-op because the next
/// connect reads <see cref="IOcrxPlugin.Rois"/> afresh.
/// </summary>
public class PluginServicesRoiUpdateTests
{
    private static readonly RoiSubscription Moved =
        new("counter", new RoiRect(1266, 490, 61, 38), 6.9, RoiKind.Text);

    [Fact]
    public async Task WithoutALiveSession_IsANoOp()
    {
        var services = new PluginServices([], new RecordingOutput(), verbose: false, dumpFrame: null);

        await services.UpdateRoisAsync([Moved]);
    }

    [Fact]
    public async Task PassesTheWholeSetToTheLiveSession()
    {
        IReadOnlyList<RoiSubscription>? sent = null;
        var services = new PluginServices([], new RecordingOutput(), verbose: false, dumpFrame: null)
        {
            UpdateRoisHandler = (rois, _) =>
            {
                sent = rois;
                return Task.CompletedTask;
            },
        };

        await services.UpdateRoisAsync([Moved]);

        Assert.Equal(Moved, Assert.Single(sent!));
    }

    /// <summary>
    /// The session can end between the handler check and the write. That is not something the
    /// plugin can act on, so it must not surface as an exception out of the plugin's own watcher.
    /// </summary>
    [Fact]
    public async Task ASessionThatEndedUnderTheWrite_IsSwallowed()
    {
        var output = new RecordingOutput();
        var services = new PluginServices([], output, verbose: true, dumpFrame: null)
        {
            UpdateRoisHandler = (_, _) => throw new ObjectDisposedException("TrackSession"),
        };

        await services.UpdateRoisAsync([Moved]);

        Assert.Contains(output.Lines, line => line.StartsWith("ROI update skipped", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CancellationByTheCaller_Propagates()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var services = new PluginServices([], new RecordingOutput(), verbose: false, dumpFrame: null)
        {
            UpdateRoisHandler = (_, ct) => Task.FromCanceled(ct),
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => services.UpdateRoisAsync([Moved], cts.Token));
    }

    [Fact]
    public async Task TrackSession_WritesAFullReplacementOnTheRequestStream()
    {
        var writer = new RecordingClientStreamWriter<TrackRequest>();
        var reader = new QueueAsyncStreamReader<TrackResponse>([]);
        using var call = new AsyncDuplexStreamingCall<TrackRequest, TrackResponse>(
            writer,
            reader,
            Task.FromResult(new Metadata()),
            () => new Status(StatusCode.OK, string.Empty),
            () => new Metadata(),
            () => { });
        await using var session = new TrackSession(call);

        await session.UpdateRoisAsync([Moved], CancellationToken.None);

        var request = Assert.Single(writer.Messages);
        Assert.Equal(TrackRequest.MsgOneofCase.Rois, request.MsgCase);
        var roi = Assert.Single(request.Rois.Rois);
        Assert.Equal(("counter", 1266u, 490u, 61u, 38u, 6.9),
            (roi.Id, roi.Rect.X, roi.Rect.Y, roi.Rect.Width, roi.Rect.Height, roi.Scale));
    }
}
