using PublicApiGenerator;
using VerifyXunit;
using Xunit;

namespace Ocrx.Contracts.Tests;

public sealed class PublicApiApprovalTests
{
    [Fact]
    public Task ApproveContractsPublicApi()
        => Verify(typeof(RoiScaler).Assembly.GeneratePublicApi());
}
