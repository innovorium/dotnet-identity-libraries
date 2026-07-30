using Marten;
using Marten.Linq;
using OpenIddict.Abstractions;

namespace Innovorium.OpenIddict.Marten;

internal static class MartenOpenIddictBulkOperations
{
    internal const int BatchSize = 1_000;
    internal const int MaximumBatches = 1_000;
    internal const int MaximumRows = BatchSize * MaximumBatches;

    internal static async ValueTask<long> RevokeAsync<TDocument>(
        IDocumentSession session,
        IQueryable<MartenOpenIddictBulkTarget> targets,
        Func<TDocument, Guid> getIdentifier,
        Func<TDocument, int> getVersion,
        Action<TDocument> revoke,
        string entityName,
        CancellationToken cancellationToken)
        where TDocument : class
    {
        var snapshot = await targets
            .Take(MaximumRows)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var result = 0L;

        foreach (var batch in snapshot.Chunk(BatchSize))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var documents = await session
                .LoadManyAsync<TDocument>(cancellationToken, batch.Select(target => target.Id))
                .ConfigureAwait(false);

            QueueRevocations(
                session,
                batch,
                documents,
                getIdentifier,
                getVersion,
                revoke,
                entityName);

            try
            {
                await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                result += documents.Count;
            }
            catch (Exception exception) when (
                OpenIddictMartenExceptionHelper.IsConcurrencyException(exception))
            {
                var identifier = documents.Count is 0
                    ? Guid.Empty
                    : getIdentifier(documents[0]);
                throw OpenIddictMartenExceptionHelper.CreateConcurrencyException(
                    entityName,
                    identifier,
                    exception);
            }

            foreach (var document in documents)
            {
                session.Eject(document);
            }
        }

        return result;
    }

    internal static void QueueRevocations<TDocument>(
        IDocumentSession session,
        IReadOnlyCollection<MartenOpenIddictBulkTarget> targets,
        IReadOnlyCollection<TDocument> documents,
        Func<TDocument, Guid> getIdentifier,
        Func<TDocument, int> getVersion,
        Action<TDocument> revoke,
        string entityName)
        where TDocument : class
    {
        var documentsByIdentifier = documents.ToDictionary(getIdentifier);

        foreach (var target in targets)
        {
            if (!documentsByIdentifier.TryGetValue(target.Id, out var document) ||
                getVersion(document) != target.Version)
            {
                throw OpenIddictMartenExceptionHelper.CreateConcurrencyException(
                    entityName,
                    target.Id,
                    new JasperFx.ConcurrencyException(typeof(TDocument), target.Id));
            }
        }

        foreach (var target in targets)
        {
            var document = documentsByIdentifier[target.Id];
            revoke(document);
            MartenOpenIddictRevisionedUpdate.Queue(
                session,
                document,
                checked(target.Version + 1));
        }
    }
}

internal sealed record MartenOpenIddictBulkTarget(Guid Id, int Version);
