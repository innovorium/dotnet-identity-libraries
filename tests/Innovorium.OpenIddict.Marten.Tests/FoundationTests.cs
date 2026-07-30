using Xunit;

namespace Innovorium.OpenIddict.Marten.Tests;

public sealed class FoundationTests
{
    [Fact]
    public void RuntimeIsDotnet10OrNewer()
    {
        Assert.True(Environment.Version.Major >= 10);
    }
}
