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
