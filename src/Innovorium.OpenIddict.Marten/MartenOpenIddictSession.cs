using System.Runtime.CompilerServices;
using Marten;

namespace Innovorium.OpenIddict.Marten;

internal static class MartenOpenIddictSession
{
    internal static async IAsyncEnumerable<TResult> QueryAsync<TResult>(
        IDocumentStore store,
        Func<IDocumentSession, CancellationToken, IAsyncEnumerable<TResult>> query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(query);

        await using var session = store.LightweightSession();
        await foreach (var result in query(session, cancellationToken)
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            yield return result;
        }
    }
}
