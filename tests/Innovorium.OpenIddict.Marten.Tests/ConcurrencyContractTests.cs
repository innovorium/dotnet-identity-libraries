using OpenIddict.Abstractions;
using Xunit;

namespace Innovorium.OpenIddict.Marten.Tests;

public sealed class ConcurrencyContractTests
{
    [Fact]
    public void BulkRevocationRejectsDocumentsChangedAfterTheTargetSnapshot()
    {
        var session = RecordingDocumentSession.CreateSession(out var recorder);
        var identifier = Guid.NewGuid();
        var token = new OpenIddictMartenToken
        {
            Id = identifier,
            Version = 2,
            Status = OpenIddictConstants.Statuses.Valid,
        };

        Assert.Throws<OpenIddictExceptions.ConcurrencyException>(() =>
            MartenOpenIddictBulkOperations.QueueRevocations(
                session,
                [new MartenOpenIddictBulkTarget(identifier, 1)],
                [token],
                static value => value.Id,
                static value => value.Version,
                static value => value.Status = OpenIddictConstants.Statuses.Revoked,
                "token"));

        Assert.Equal(OpenIddictConstants.Statuses.Valid, token.Status);
        Assert.Empty(recorder.TakeMutationCalls());
    }

    [Fact]
    public async Task UpdateTranslatesNestedMartenConcurrencyFailure()
    {
        var session = RecordingDocumentSession.Create(out var recorder);
        var nested = new InvalidOperationException(
            "Marten batch failed.",
            new AggregateException(new JasperFx.ConcurrencyException(typeof(object), Guid.NewGuid())));
        recorder.SaveChangesException = nested;
        var store = new MartenOpenIddictScopeStore(session);

        var exception = await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(async () =>
            await store.UpdateAsync(
                new OpenIddictMartenScope { Version = 1 },
                TestContext.Current.CancellationToken));

        Assert.Same(nested, exception.InnerException);
    }

    [Fact]
    public async Task DeleteTranslatesNestedMartenConcurrencyFailure()
    {
        var session = RecordingDocumentSession.Create(out var recorder);
        recorder.ExecuteException = new AggregateException(
            new JasperFx.ConcurrencyException(typeof(OpenIddictMartenScope), Guid.NewGuid()));
        var store = new MartenOpenIddictScopeStore(session);

        await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(async () =>
            await store.DeleteAsync(
                new OpenIddictMartenScope { Version = 1 },
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ApplicationDeleteTranslatesZeroAffectedRowToConcurrency()
    {
        var documentStore = RecordingDocumentSession.Create(out var recorder);
        recorder.ExecuteResult = 0;
        var store = new MartenOpenIddictApplicationStore(documentStore);

        var exception = await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(async () =>
            await store.DeleteAsync(
                new OpenIddictMartenApplication { Id = Guid.NewGuid(), Version = 1 },
                TestContext.Current.CancellationToken));

        Assert.IsType<JasperFx.ConcurrencyException>(exception.InnerException);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task MutationsPreserveCancellationAndUnknownFailures(bool update, bool cancellation)
    {
        var session = RecordingDocumentSession.Create(out var recorder);
        Exception failure = cancellation
            ? new OperationCanceledException(TestContext.Current.CancellationToken)
            : new InvalidOperationException("Database unavailable.");

        if (update)
        {
            recorder.SaveChangesException = failure;
        }
        else
        {
            recorder.ExecuteException = failure;
        }

        var thrown = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            var store = new MartenOpenIddictScopeStore(session);
            if (update)
            {
                await store.UpdateAsync(
                    new OpenIddictMartenScope { Version = 1 },
                    TestContext.Current.CancellationToken);
            }
            else
            {
                await store.DeleteAsync(
                    new OpenIddictMartenScope { Version = 1 },
                    TestContext.Current.CancellationToken);
            }
        });

        Assert.Same(failure, thrown);
    }
}
