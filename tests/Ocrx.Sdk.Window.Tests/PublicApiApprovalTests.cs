using PublicApiGenerator;
using VerifyXunit;
using Xunit;

namespace Ocrx.Sdk.Window.Tests;

public sealed class PublicApiApprovalTests
{
    [Fact]
    public Task ApproveSdkWindowPublicApi()
        => Verify(typeof(PluginWindowHost).Assembly.GeneratePublicApi());
}
