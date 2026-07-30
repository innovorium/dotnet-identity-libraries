using Xunit;

namespace Innovorium.OpenIddict.Marten.Tests;

public sealed class PersistenceContractTests
{
    [Fact]
    public async Task ApplicationMutationsCommitTheirOwnedSessionImmediately()
    {
        var session = RecordingDocumentSession.Create(out var recorder);
        var store = new MartenOpenIddictApplicationStore(session);
        var application = new OpenIddictMartenApplication();

        await store.CreateAsync(application, TestContext.Current.CancellationToken);
        Assert.Equal(["Insert", "SaveChangesAsync"], recorder.TakeMutationCalls());

        await store.UpdateAsync(application, TestContext.Current.CancellationToken);
        Assert.Equal(["Update", "SaveChangesAsync"], recorder.TakeMutationCalls());

        await store.DeleteAsync(application, TestContext.Current.CancellationToken);
        Assert.Equal(["ExecuteAsync"], recorder.TakeMutationCalls());
        Assert.Contains("mt_doc_openiddict_application", recorder.LastCommandText, StringComparison.Ordinal);
        Assert.Equal(application.Id, recorder.LastCommandParameters["id"]);
        Assert.Equal(application.Version, recorder.LastCommandParameters["version"]);
    }

    [Fact]
    public async Task ScopeMutationsCommitTheirOwnedSessionImmediately()
    {
        var session = RecordingDocumentSession.Create(out var recorder);
        var store = new MartenOpenIddictScopeStore(session);
        var scope = new OpenIddictMartenScope();

        await store.CreateAsync(scope, TestContext.Current.CancellationToken);
        Assert.Equal(["Insert", "SaveChangesAsync"], recorder.TakeMutationCalls());

        await store.UpdateAsync(scope, TestContext.Current.CancellationToken);
        Assert.Equal(["Update", "SaveChangesAsync"], recorder.TakeMutationCalls());

        await store.DeleteAsync(scope, TestContext.Current.CancellationToken);
        Assert.Equal(["ExecuteAsync"], recorder.TakeMutationCalls());
        Assert.Contains("mt_doc_openiddict_scope", recorder.LastCommandText, StringComparison.Ordinal);
        Assert.Equal(scope.Id, recorder.LastCommandParameters["id"]);
        Assert.Equal(scope.Version, recorder.LastCommandParameters["version"]);
    }

    [Fact]
    public async Task AuthorizationMutationsCommitTheirOwnedSessionImmediately()
    {
        var session = RecordingDocumentSession.Create(out var recorder);
        var store = new MartenOpenIddictAuthorizationStore(session);
        var authorization = new OpenIddictMartenAuthorization();

        await store.CreateAsync(authorization, TestContext.Current.CancellationToken);
        Assert.Equal(["Insert", "SaveChangesAsync"], recorder.TakeMutationCalls());

        await store.UpdateAsync(authorization, TestContext.Current.CancellationToken);
        Assert.Equal(["Update", "SaveChangesAsync"], recorder.TakeMutationCalls());

        await store.DeleteAsync(authorization, TestContext.Current.CancellationToken);
        Assert.Equal(["ExecuteAsync"], recorder.TakeMutationCalls());
        Assert.Contains("mt_doc_openiddict_authorization", recorder.LastCommandText, StringComparison.Ordinal);
        Assert.Equal(authorization.Id, recorder.LastCommandParameters["id"]);
        Assert.Equal(authorization.Version, recorder.LastCommandParameters["version"]);
    }

    [Fact]
    public async Task TokenMutationsCommitTheirOwnedSessionImmediately()
    {
        var session = RecordingDocumentSession.Create(out var recorder);
        var store = new MartenOpenIddictTokenStore(session, TimeProvider.System);
        var token = new OpenIddictMartenToken();

        await store.CreateAsync(token, TestContext.Current.CancellationToken);
        Assert.Equal(["Insert", "SaveChangesAsync"], recorder.TakeMutationCalls());

        await store.UpdateAsync(token, TestContext.Current.CancellationToken);
        Assert.Equal(["Update", "SaveChangesAsync"], recorder.TakeMutationCalls());

        await store.DeleteAsync(token, TestContext.Current.CancellationToken);
        Assert.Equal(["ExecuteAsync"], recorder.TakeMutationCalls());
        Assert.Contains("mt_doc_openiddict_token", recorder.LastCommandText, StringComparison.Ordinal);
        Assert.Equal(token.Id, recorder.LastCommandParameters["id"]);
        Assert.Equal(token.Version, recorder.LastCommandParameters["version"]);
    }
}
