using Xunit;

namespace Innovorium.AspNetCore.Identity.Marten.Tests;

public sealed class FoundationTests
{
    [Fact]
    public void RuntimeIsDotnet10OrNewer()
    {
        Assert.True(Environment.Version.Major >= 10);
    }
}
