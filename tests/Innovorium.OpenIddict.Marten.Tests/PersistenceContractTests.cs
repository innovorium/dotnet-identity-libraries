using Xunit;

namespace Innovorium.OpenIddict.Marten.Tests;

public sealed class PersistenceContractTests
{
    [Fact]
    public async Task ApplicationMutationsFlushImmediately()
    {
        var session = RecordingDocumentSession.Create(out var recorder);
        var store = new MartenOpenIddictApplicationStore(session);
        var application = new OpenIddictMartenApplication();

        await store.CreateAsync(application, TestContext.Current.CancellationToken);
        Assert.Equal(["Insert", "SaveChangesAsync"], recorder.TakeMutationCalls());

        await store.UpdateAsync(application, TestContext.Current.CancellationToken);
        Assert.Equal(["UpdateRevision", "SaveChangesAsync"], recorder.TakeMutationCalls());

        await store.DeleteAsync(application, TestContext.Current.CancellationToken);
        Assert.Equal(["ExecuteAsync", "Eject"], recorder.TakeMutationCalls());
        Assert.Contains("mt_doc_openiddict_application", recorder.LastCommandText, StringComparison.Ordinal);
        Assert.Equal(application.Id, recorder.LastCommandParameters["id"]);
        Assert.Equal(application.Version, recorder.LastCommandParameters["version"]);
    }

    [Fact]
    public async Task ScopeMutationsFlushImmediately()
    {
        var session = RecordingDocumentSession.Create(out var recorder);
        var store = new MartenOpenIddictScopeStore(session);
        var scope = new OpenIddictMartenScope();

        await store.CreateAsync(scope, TestContext.Current.CancellationToken);
        Assert.Equal(["Insert", "SaveChangesAsync"], recorder.TakeMutationCalls());

        await store.UpdateAsync(scope, TestContext.Current.CancellationToken);
        Assert.Equal(["UpdateRevision", "SaveChangesAsync"], recorder.TakeMutationCalls());

        await store.DeleteAsync(scope, TestContext.Current.CancellationToken);
        Assert.Equal(["ExecuteAsync", "Eject"], recorder.TakeMutationCalls());
        Assert.Contains("mt_doc_openiddict_scope", recorder.LastCommandText, StringComparison.Ordinal);
        Assert.Equal(scope.Id, recorder.LastCommandParameters["id"]);
        Assert.Equal(scope.Version, recorder.LastCommandParameters["version"]);
    }
}
