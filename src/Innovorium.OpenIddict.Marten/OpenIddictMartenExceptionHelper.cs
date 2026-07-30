using OpenIddict.Abstractions;

namespace Innovorium.OpenIddict.Marten;

internal static class OpenIddictMartenExceptionHelper
{
    internal static bool IsConcurrencyException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is JasperFx.ConcurrencyException or global::Marten.Exceptions.NonExistentDocumentException)
        {
            return true;
        }

        if (exception is AggregateException aggregate &&
            aggregate.InnerExceptions.Any(IsConcurrencyException))
        {
            return true;
        }

        return exception.InnerException is not null &&
            IsConcurrencyException(exception.InnerException);
    }

    internal static OpenIddictExceptions.ConcurrencyException CreateConcurrencyException(
        string entityName,
        Guid identifier,
        Exception exception)
        => new(
            $"The OpenIddict {entityName} '{identifier:D}' was concurrently modified.",
            exception);
}
