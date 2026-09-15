using Grpc.Core;
using Ocrx.Contracts.Proto;
using Xunit;

namespace Ocrx.Sdk.Tests;

public class TrackSessionSettingsTests
{
    [Fact]
    public async Task ApplySettings_IsDispatchedWithoutYieldingATick_AndSettingsSpecUsesTheLiveStream()
    {
        var writer = new RecordingClientStreamWriter<TrackRequest>();
        var reader = new QueueAsyncStreamReader<TrackResponse>(
        [
            new TrackResponse { ApplySettings = new ApplySettings([new SettingsValue("theme", "dark")]).ToProto() },
            new TrackResponse { Tick = new TickResult { TimestampMs = 1_000, FrameSeq = 7 } },
        ]);
        using var call = new AsyncDuplexStreamingCall<TrackRequest, TrackResponse>(
            writer,
            reader,
            Task.FromResult(new Metadata()),
            () => new Status(StatusCode.OK, string.Empty),
            () => new Metadata(),
            () => { });
        await using var session = new TrackSession(call);
        ApplySettings? received = null;
        session.ApplySettingsHandler = (apply, _) =>
        {
            received = apply;
            return Task.CompletedTask;
        };

        await session.PublishSettingsAsync(
            new SettingsSpec([new SettingsField("theme", "Theme", SettingsFieldType.String, "dark")]));
        var ticks = new List<TickData>();
        await foreach (var tick in session.Ticks(CancellationToken.None))
            ticks.Add(tick);

        var applied = Assert.IsType<ApplySettings>(received);
        var value = Assert.Single(applied.Values);
        Assert.Equal(("theme", "dark"), (value.Id, value.Value));
        var tickResult = Assert.Single(ticks);
        Assert.Equal(7ul, tickResult.FrameSeq);
        var request = Assert.Single(writer.Messages);
        Assert.Equal(TrackRequest.MsgOneofCase.Settings, request.MsgCase);
        Assert.Equal("theme", Assert.Single(request.Settings.Fields).Id);
    }
}
