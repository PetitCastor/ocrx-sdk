using Ocrx.Sdk;
using Ocrx.Sdk.Testing;
using Xunit;

namespace Ocrx.Sdk.Testing.Tests;

public class FakeRecordSinkTests
{
    [Fact]
    public async Task EmitAsync_RecordsEveryRecord_InOrder()
    {
        var sink = new FakeRecordSink();
        var observation = new CaptureRecord(DateTime.UtcNow, "tracker", TriggerKind.Auto, "ready");
        var cleared = new CaptureRecord(DateTime.UtcNow, "tracker", TriggerKind.Auto, "")
        {
            Kind = RecordKind.Cleared,
        };

        await sink.EmitAsync(observation, CancellationToken.None);
        await sink.EmitAsync(cleared, CancellationToken.None);
        await sink.DisposeAsync();

        Assert.Equal([observation, cleared], sink.Received);
    }
}
