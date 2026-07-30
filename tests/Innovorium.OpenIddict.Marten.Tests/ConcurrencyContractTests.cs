using Marten.Exceptions;
using OpenIddict.Abstractions;
using Xunit;

namespace Innovorium.OpenIddict.Marten.Tests;

public sealed class ConcurrencyContractTests
{
    [Theory]
    [InlineData("application")]
    [InlineData("scope")]
    public async Task StoresTranslateNestedMartenConcurrencyFailures(string entity)
    {
        var session = RecordingDocumentSession.Create(out var recorder);
        var nested = new InvalidOperationException(
            "Marten batch failed.",
            new AggregateException(new JasperFx.ConcurrencyException(typeof(object), Guid.NewGuid())));
        recorder.SaveChangesException = nested;

        OpenIddictExceptions.ConcurrencyException exception;
        if (entity == "application")
        {
            var store = new MartenOpenIddictApplicationStore(session);
            exception = await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(async () =>
                await store.UpdateAsync(
                    new OpenIddictMartenApplication { Version = 1 },
                    TestContext.Current.CancellationToken));
        }
        else
        {
            var store = new MartenOpenIddictScopeStore(session);
            exception = await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(async () =>
                await store.UpdateAsync(
                    new OpenIddictMartenScope { Version = 1 },
                    TestContext.Current.CancellationToken));
        }

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

    [Theory]
    [InlineData("application")]
    [InlineData("scope")]
    public async Task UpdatesTranslateMissingDocumentFailures(string entity)
    {
        var session = RecordingDocumentSession.Create(out var recorder);
        var id = Guid.NewGuid();
        recorder.SaveChangesException = new NonExistentDocumentException(typeof(object), id);

        OpenIddictExceptions.ConcurrencyException exception;
        if (entity == "application")
        {
            exception = await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(async () =>
                await new MartenOpenIddictApplicationStore(session).UpdateAsync(
                    new OpenIddictMartenApplication { Id = id, Version = 1 },
                    TestContext.Current.CancellationToken));
        }
        else
        {
            exception = await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(async () =>
                await new MartenOpenIddictScopeStore(session).UpdateAsync(
                    new OpenIddictMartenScope { Id = id, Version = 1 },
                    TestContext.Current.CancellationToken));
        }

        Assert.IsType<NonExistentDocumentException>(exception.InnerException);
    }

    [Theory]
    [InlineData("application")]
    [InlineData("scope")]
    public async Task DeleteTranslatesZeroAffectedRows(string entity)
    {
        var session = RecordingDocumentSession.Create(out var recorder);
        recorder.ExecuteResult = 0;

        if (entity == "application")
        {
            await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(async () =>
                await new MartenOpenIddictApplicationStore(session).DeleteAsync(
                    new OpenIddictMartenApplication { Version = 1 },
                    TestContext.Current.CancellationToken));
        }
        else
        {
            await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(async () =>
                await new MartenOpenIddictScopeStore(session).DeleteAsync(
                    new OpenIddictMartenScope { Version = 1 },
                    TestContext.Current.CancellationToken));
        }

        Assert.DoesNotContain("Eject", recorder.TakeMutationCalls());
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
