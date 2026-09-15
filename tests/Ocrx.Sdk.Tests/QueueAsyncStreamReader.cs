using Grpc.Core;

namespace Ocrx.Sdk.Tests;

internal sealed class QueueAsyncStreamReader<T>(IEnumerable<T> messages) : IAsyncStreamReader<T>
{
    private readonly Queue<T> _messages = new(messages);

    public T Current { get; private set; } = default!;

    public Task<bool> MoveNext(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_messages.TryDequeue(out var message))
            return Task.FromResult(false);

        Current = message;
        return Task.FromResult(true);
    }
}
