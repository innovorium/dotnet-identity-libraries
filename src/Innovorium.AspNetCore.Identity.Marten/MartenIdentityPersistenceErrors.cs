namespace Innovorium.AspNetCore.Identity.Marten;

internal static class MartenIdentityPersistenceErrors
{
    public static bool IsConcurrencyFailure(Exception exception) =>
        Traverse(exception).Any(current => current.GetType().FullName is
            "JasperFx.ConcurrencyException" or
            "Marten.Exceptions.ConcurrentUpdateException");

    public static string? FindUniqueConstraint(Exception exception)
        => FindPostgreSqlConstraint(exception, "23505");

    public static string? FindForeignKeyConstraint(Exception exception)
        => FindPostgreSqlConstraint(exception, "23503");

    private static string? FindPostgreSqlConstraint(Exception exception, string expectedSqlState)
    {
        foreach (var current in Traverse(exception))
        {
            if (current.GetType().FullName != "Npgsql.PostgresException")
            {
                continue;
            }

            var sqlState = current.GetType().GetProperty("SqlState")?.GetValue(current) as string;
            if (!string.Equals(sqlState, expectedSqlState, StringComparison.Ordinal))
            {
                continue;
            }

            return current.GetType().GetProperty("ConstraintName")?.GetValue(current) as string;
        }

        return null;
    }

    private static IEnumerable<Exception> Traverse(Exception exception)
    {
        var pending = new Stack<Exception>();
        pending.Push(exception);

        while (pending.TryPop(out var current))
        {
            yield return current;

            if (current is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions)
                {
                    pending.Push(inner);
                }
            }
            else if (current.InnerException is { } inner)
            {
                pending.Push(inner);
            }
        }
    }
}
