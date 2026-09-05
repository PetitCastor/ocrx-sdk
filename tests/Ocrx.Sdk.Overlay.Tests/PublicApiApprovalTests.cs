using PublicApiGenerator;
using VerifyXunit;
using Xunit;

namespace Ocrx.Sdk.Overlay.Tests;

public sealed class PublicApiApprovalTests
{
    [Fact]
    public Task ApproveSdkOverlayPublicApi()
        => Verify(typeof(OverlaySinkFactory).Assembly.GeneratePublicApi());
}
