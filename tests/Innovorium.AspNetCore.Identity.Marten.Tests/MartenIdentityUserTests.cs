using JasperFx.Metadata;
using Xunit;

namespace Innovorium.AspNetCore.Identity.Marten.Tests;

public sealed class MartenIdentityUserTests
{
    [Fact]
    public void UserIsExtensibleAndVersioned()
    {
        var user = new ApplicationUser
        {
            Id = "user-1",
            DisplayName = "Ada",
            Version = Guid.NewGuid(),
        };

        Assert.IsAssignableFrom<IVersioned>(user);
        Assert.Equal("user-1", user.Id);
        Assert.Equal("Ada", user.DisplayName);
        Assert.NotEqual(Guid.Empty, user.Version);
    }

    private sealed class ApplicationUser : MartenIdentityUser
    {
        public string DisplayName { get; init; } = string.Empty;
    }
}
