using Marten;
using Weasel.Storage;

namespace Innovorium.OpenIddict.Marten;

internal static class MartenOpenIddictRevisionedUpdate
{
    internal static void Queue<TDocument>(
        IDocumentSession session,
        TDocument document,
        int expectedVersion)
        where TDocument : notnull
    {
        session.Update(document);

        var operation = session.PendingChanges
            .OperationsFor<TDocument>()
            .OfType<IDocumentStorageOperation>()
            .Single(candidate => ReferenceEquals(candidate.Document, document));

        if (operation is not IRevisionedOperation revisioned)
        {
            throw new InvalidOperationException(
                $"The Marten mapping for '{typeof(TDocument).FullName}' does not use numeric revisions.");
        }

        revisioned.Revision = expectedVersion;
    }
}
