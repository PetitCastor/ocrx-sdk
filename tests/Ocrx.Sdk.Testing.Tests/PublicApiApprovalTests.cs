using PublicApiGenerator;
using VerifyXunit;
using Xunit;

namespace Ocrx.Sdk.Testing.Tests;

public sealed class PublicApiApprovalTests
{
    [Fact]
    public Task ApproveSdkTestingPublicApi()
        => Verify(typeof(TickDataBuilder).Assembly.GeneratePublicApi());
}
