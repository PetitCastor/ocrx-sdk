using Ocrx.Contracts;

namespace Ocrx.Sdk.Tests;

/// <summary>Only the two non-defaulted members: everything else is the interface default an old
/// plugin gets.</summary>
internal sealed class BareboneStubPlugin : IOcrxPlugin
{
    public string Name => "barebone";
    public IReadOnlyList<RoiSubscription> Rois => [];
    public Task OnTickAsync(TickContext ctx, CancellationToken ct) => Task.CompletedTask;
}
