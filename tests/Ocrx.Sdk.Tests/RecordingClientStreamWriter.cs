using Grpc.Core;

namespace Ocrx.Sdk.Tests;

internal sealed class RecordingClientStreamWriter<T> : IClientStreamWriter<T>
{
    public List<T> Messages { get; } = [];

    public int CompleteCalls { get; private set; }

    public WriteOptions? WriteOptions { get; set; }

    public Task WriteAsync(T message)
    {
        Messages.Add(message);
        return Task.CompletedTask;
    }

    public Task CompleteAsync()
    {
        CompleteCalls++;
        return Task.CompletedTask;
    }
}
