using PublicApiGenerator;
using VerifyXunit;
using Xunit;

namespace Ocrx.Sdk.Tests;

public sealed class PublicApiApprovalTests
{
    [Fact]
    public Task ApproveSdkPublicApi()
        => Verify(typeof(IOcrxPlugin).Assembly.GeneratePublicApi(new ApiGeneratorOptions
        {
            ExcludeAttributes =
            [
                "System.Runtime.CompilerServices.AsyncIteratorStateMachineAttribute",
            ],
        }));
}
