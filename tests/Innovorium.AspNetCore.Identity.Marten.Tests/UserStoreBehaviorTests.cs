using Marten;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Xunit;

namespace Innovorium.AspNetCore.Identity.Marten.Tests;

public sealed class UserStoreBehaviorTests
{
    private const string TestConnectionString =
        "Host=127.0.0.1;Port=1;Database=identity_tests;Username=identity_tests;Password=identity_tests;Timeout=1";

    [Fact]
    public async Task CoreAccountStateRoundTripsThroughTheIdentityInterfaces()
    {
        using var documentStore = DocumentStore.For(options => options.Connection(TestConnectionString));
        using var store = CreateStore(documentStore);
        var user = new ApplicationUser();
        var cancellationToken = TestContext.Current.CancellationToken;

        await store.SetUserNameAsync(user, "ada", cancellationToken);
        await store.SetNormalizedUserNameAsync(user, "ADA", cancellationToken);
        await store.SetEmailAsync(user, "ada@example.test", cancellationToken);
        await store.SetNormalizedEmailAsync(user, "ADA@EXAMPLE.TEST", cancellationToken);
        await store.SetEmailConfirmedAsync(user, true, cancellationToken);
        await store.SetPasswordHashAsync(user, "password-hash", cancellationToken);
        await store.SetPhoneNumberAsync(user, "+12025550123", cancellationToken);
        await store.SetPhoneNumberConfirmedAsync(user, true, cancellationToken);
        await store.SetSecurityStampAsync(user, "security-stamp", cancellationToken);
        await store.SetLockoutEnabledAsync(user, true, cancellationToken);
        var lockoutEnd = DateTimeOffset.UtcNow.AddMinutes(5);
        await store.SetLockoutEndDateAsync(user, lockoutEnd, cancellationToken);
        await store.IncrementAccessFailedCountAsync(user, cancellationToken);
        await store.SetTwoFactorEnabledAsync(user, true, cancellationToken);

        Assert.Equal("ada", await store.GetUserNameAsync(user, cancellationToken));
        Assert.Equal("ADA", await store.GetNormalizedUserNameAsync(user, cancellationToken));
        Assert.Equal("ada@example.test", await store.GetEmailAsync(user, cancellationToken));
        Assert.Equal("ADA@EXAMPLE.TEST", await store.GetNormalizedEmailAsync(user, cancellationToken));
        Assert.True(await store.GetEmailConfirmedAsync(user, cancellationToken));
        Assert.Equal("password-hash", await store.GetPasswordHashAsync(user, cancellationToken));
        Assert.True(await store.HasPasswordAsync(user, cancellationToken));
        Assert.Equal("+12025550123", await store.GetPhoneNumberAsync(user, cancellationToken));
        Assert.True(await store.GetPhoneNumberConfirmedAsync(user, cancellationToken));
        Assert.Equal("security-stamp", await store.GetSecurityStampAsync(user, cancellationToken));
        Assert.True(await store.GetLockoutEnabledAsync(user, cancellationToken));
        Assert.Equal(lockoutEnd, await store.GetLockoutEndDateAsync(user, cancellationToken));
        Assert.Equal(1, await store.GetAccessFailedCountAsync(user, cancellationToken));
        Assert.True(await store.GetTwoFactorEnabledAsync(user, cancellationToken));

        await store.ResetAccessFailedCountAsync(user, cancellationToken);
        Assert.Equal(0, await store.GetAccessFailedCountAsync(user, cancellationToken));
    }

    [Fact]
    public async Task CancelledOperationsDoNotMutateUserState()
    {
        using var documentStore = DocumentStore.For(options => options.Connection(TestConnectionString));
        using var store = CreateStore(documentStore);
        var user = new ApplicationUser { UserName = "original" };

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            store.SetUserNameAsync(user, "changed", new CancellationToken(canceled: true)));

        Assert.Equal("original", user.UserName);
    }

    [Fact]
    public async Task DisposedStoreRejectsFurtherUse()
    {
        using var documentStore = DocumentStore.For(options => options.Connection(TestConnectionString));
        var store = CreateStore(documentStore);
        store.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            store.GetUserIdAsync(new ApplicationUser(), TestContext.Current.CancellationToken));
    }

    private static MartenUserOnlyStore<ApplicationUser> CreateStore(IDocumentStore documentStore) =>
        new(documentStore, Options.Create(new IdentityOptions()), new IdentityErrorDescriber());

    private sealed class ApplicationUser : MartenIdentityUser;
}
