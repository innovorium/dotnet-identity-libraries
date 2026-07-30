using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using JasperFx.MultiTenancy;
using Marten;
using Marten.Linq;
using Marten.Schema;
using Npgsql;
using NpgsqlTypes;
using OpenIddict.Abstractions;

namespace Innovorium.OpenIddict.Marten;

internal sealed class MartenOpenIddictAuthorizationStore(
    IDocumentStore store) : IOpenIddictAuthorizationStore<OpenIddictMartenAuthorization>
{
    private readonly IDocumentStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async ValueTask<long> CountAsync(CancellationToken cancellationToken)
    {
        await using var session = _store.LightweightSession();
        return await session.Query<OpenIddictMartenAuthorization>()
            .LongCountAsync(cancellationToken)
            .ConfigureAwait(false);
    }

#pragma warning disable CS8714 // OpenIddict leaves TResult unconstrained; Marten's async LINQ API adds notnull.
    public async ValueTask<long> CountAsync<TResult>(
        Func<IQueryable<OpenIddictMartenAuthorization>, IQueryable<TResult>> query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        await using var session = _store.LightweightSession();
        return await query(session.Query<OpenIddictMartenAuthorization>())
            .LongCountAsync(cancellationToken)
            .ConfigureAwait(false);
    }
#pragma warning restore CS8714

    public async ValueTask CreateAsync(
        OpenIddictMartenAuthorization authorization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        await using var session = _store.LightweightSession();
        session.Insert(authorization);
        await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DeleteAsync(
        OpenIddictMartenAuthorization authorization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        var mapping = GetSingleTenantedMapping<OpenIddictMartenAuthorization>("authorization");
        using var command = new NpgsqlCommand(
            $"delete from {mapping.TableName.QualifiedName} where id = @id and mt_version = @version");
        command.Parameters.Add("id", NpgsqlDbType.Uuid).Value = authorization.Id;
        command.Parameters.Add("version", NpgsqlDbType.Integer).Value = authorization.Version;

        await using var session = _store.LightweightSession();
        try
        {
            var affectedRows = await session.ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
            if (affectedRows != 1)
            {
                throw new JasperFx.ConcurrencyException(
                    typeof(OpenIddictMartenAuthorization),
                    authorization.Id);
            }
        }
        catch (Exception exception) when (
            OpenIddictMartenExceptionHelper.IsConcurrencyException(exception))
        {
            throw OpenIddictMartenExceptionHelper.CreateConcurrencyException(
                "authorization",
                authorization.Id,
                exception);
        }
    }

    public IAsyncEnumerable<OpenIddictMartenAuthorization> FindAsync(
        string? subject,
        string? client,
        string? status,
        string? type,
        ImmutableArray<string>? scopes,
        CancellationToken cancellationToken)
    {
        var requiredScopes = scopes is { } values && !values.IsDefaultOrEmpty
            ? values.ToArray()
            : [];

        if (!string.IsNullOrEmpty(client) && !Guid.TryParse(client, out _))
        {
            return EmptyAsync(cancellationToken);
        }

        return FilterAsync(
            MartenOpenIddictSession.QueryAsync(
                _store,
                (session, token) =>
                {
                    IQueryable<OpenIddictMartenAuthorization> query = session
                        .Query<OpenIddictMartenAuthorization>();

                    if (!string.IsNullOrEmpty(subject))
                    {
                        query = query.Where(authorization => authorization.Subject == subject);
                    }

                    if (!string.IsNullOrEmpty(client))
                    {
                        var applicationId = Guid.Parse(client);
                        query = query.Where(authorization => authorization.ApplicationId == applicationId);
                    }

                    if (!string.IsNullOrEmpty(status))
                    {
                        query = query.Where(authorization => authorization.Status == status);
                    }

                    if (!string.IsNullOrEmpty(type))
                    {
                        query = query.Where(authorization => authorization.Type == type);
                    }

                    foreach (var scope in requiredScopes)
                    {
                        query = query.Where(authorization => authorization.Scopes.Contains(scope));
                    }

                    return query.ToAsyncEnumerable(token);
                },
                cancellationToken),
            authorization =>
                (string.IsNullOrEmpty(subject) || string.Equals(authorization.Subject, subject, StringComparison.Ordinal)) &&
                (string.IsNullOrEmpty(status) || string.Equals(authorization.Status, status, StringComparison.Ordinal)) &&
                (string.IsNullOrEmpty(type) || string.Equals(authorization.Type, type, StringComparison.Ordinal)) &&
                requiredScopes.All(scope => authorization.Scopes.Contains(scope, StringComparer.Ordinal)),
            cancellationToken);
    }

    public IAsyncEnumerable<OpenIddictMartenAuthorization> FindByApplicationIdAsync(
        string identifier,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(identifier);

        return Guid.TryParse(identifier, out var applicationId)
            ? MartenOpenIddictSession.QueryAsync(
                _store,
                (session, token) => session.Query<OpenIddictMartenAuthorization>()
                    .Where(authorization => authorization.ApplicationId == applicationId)
                    .ToAsyncEnumerable(token),
                cancellationToken)
            : EmptyAsync(cancellationToken);
    }

    public async ValueTask<OpenIddictMartenAuthorization?> FindByIdAsync(
        string identifier,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(identifier);

        if (!Guid.TryParse(identifier, out var id))
        {
            return null;
        }

        await using var session = _store.LightweightSession();
        return await session.LoadAsync<OpenIddictMartenAuthorization>(id, cancellationToken).ConfigureAwait(false);
    }

    public IAsyncEnumerable<OpenIddictMartenAuthorization> FindBySubjectAsync(
        string subject,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(subject);

        return MartenOpenIddictSession.QueryAsync(
            _store,
            (session, token) => session.Query<OpenIddictMartenAuthorization>()
                .Where(authorization => authorization.Subject == subject)
                .ToAsyncEnumerable(token),
            cancellationToken);
    }

    public ValueTask<string?> GetApplicationIdAsync(
        OpenIddictMartenAuthorization authorization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        return ValueTask.FromResult(authorization.ApplicationId?.ToString("D", CultureInfo.InvariantCulture));
    }

#pragma warning disable CS8714 // OpenIddict leaves TResult unconstrained; Marten's async LINQ API adds notnull.
    public async ValueTask<TResult?> GetAsync<TState, TResult>(
        Func<IQueryable<OpenIddictMartenAuthorization>, TState, IQueryable<TResult>> query,
        TState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        await using var session = _store.LightweightSession();
        return await query(session.Query<OpenIddictMartenAuthorization>(), state)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }
#pragma warning restore CS8714

    public ValueTask<DateTimeOffset?> GetCreationDateAsync(
        OpenIddictMartenAuthorization authorization,
        CancellationToken cancellationToken)
        => GetValue(authorization, static value => value.CreationDate);

    public ValueTask<string?> GetIdAsync(
        OpenIddictMartenAuthorization authorization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        return ValueTask.FromResult<string?>(authorization.Id.ToString("D", CultureInfo.InvariantCulture));
    }

    public ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(
        OpenIddictMartenAuthorization authorization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        return ValueTask.FromResult((authorization.Properties ?? [])
            .ToImmutableDictionary(
                pair => pair.Key,
                pair => pair.Value.Clone(),
                StringComparer.Ordinal));
    }

    public ValueTask<ImmutableArray<string>> GetScopesAsync(
        OpenIddictMartenAuthorization authorization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        return ValueTask.FromResult((authorization.Scopes ?? []).ToImmutableArray());
    }

    public ValueTask<string?> GetStatusAsync(
        OpenIddictMartenAuthorization authorization,
        CancellationToken cancellationToken)
        => GetValue(authorization, static value => value.Status);

    public ValueTask<string?> GetSubjectAsync(
        OpenIddictMartenAuthorization authorization,
        CancellationToken cancellationToken)
        => GetValue(authorization, static value => value.Subject);

    public ValueTask<string?> GetTypeAsync(
        OpenIddictMartenAuthorization authorization,
        CancellationToken cancellationToken)
        => GetValue(authorization, static value => value.Type);

    public ValueTask<OpenIddictMartenAuthorization> InstantiateAsync(CancellationToken cancellationToken)
        => ValueTask.FromResult(new OpenIddictMartenAuthorization());

    public IAsyncEnumerable<OpenIddictMartenAuthorization> ListAsync(
        int? count,
        int? offset,
        CancellationToken cancellationToken)
    {
        return MartenOpenIddictSession.QueryAsync(
            _store,
            (session, token) =>
            {
                IQueryable<OpenIddictMartenAuthorization> query = session
                    .Query<OpenIddictMartenAuthorization>()
                    .OrderBy(authorization => authorization.Id);

                if (offset is not null)
                {
                    query = query.Skip(offset.Value);
                }

                if (count is not null)
                {
                    query = query.Take(count.Value);
                }

                return query.ToAsyncEnumerable(token);
            },
            cancellationToken);
    }

#pragma warning disable CS8714 // OpenIddict leaves TResult unconstrained; Marten's async LINQ API adds notnull.
    public IAsyncEnumerable<TResult> ListAsync<TState, TResult>(
        Func<IQueryable<OpenIddictMartenAuthorization>, TState, IQueryable<TResult>> query,
        TState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return MartenOpenIddictSession.QueryAsync(
            _store,
            (session, token) => query(session.Query<OpenIddictMartenAuthorization>(), state)
                .ToAsyncEnumerable(token),
            cancellationToken);
    }
#pragma warning restore CS8714

    public async ValueTask<long> PruneAsync(
        DateTimeOffset threshold,
        CancellationToken cancellationToken)
    {
        var authorizationMapping = GetSingleTenantedMapping<OpenIddictMartenAuthorization>("authorization");
        var tokenMapping = GetSingleTenantedMapping<OpenIddictMartenToken>("token");
        var creationDate = GetIndexedColumn(authorizationMapping, MartenOpenIddictSchema.AuthorizationCreationDateIndex);
        var status = GetIndexedColumn(authorizationMapping, MartenOpenIddictSchema.AuthorizationStatusIndex);
        var type = GetIndexedColumn(authorizationMapping, MartenOpenIddictSchema.AuthorizationTypeIndex);
        var tokenAuthorizationId = GetIndexedColumn(tokenMapping, MartenOpenIddictSchema.TokenAuthorizationIdIndex);
        var result = 0L;

        await using var session = _store.LightweightSession();
        for (var index = 0; index < MartenOpenIddictBulkOperations.MaximumBatches; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var command = new NpgsqlCommand($$"""
                with candidates as (
                    select a.id
                    from {{authorizationMapping.TableName.QualifiedName}} as a
                    where a.{{creationDate}} < @threshold
                      and (a.{{status}} is distinct from @valid
                           or a.{{type}} = @ad_hoc)
                      and not exists (
                          select 1
                          from {{tokenMapping.TableName.QualifiedName}} as t
                          where t.{{tokenAuthorizationId}} = a.id)
                    order by a.id
                    limit @batch_size
                    for update of a skip locked
                )
                delete from {{authorizationMapping.TableName.QualifiedName}} as a
                using candidates
                where a.id = candidates.id
                """);
            command.Parameters.Add("threshold", NpgsqlDbType.TimestampTz).Value = threshold.ToUniversalTime();
            command.Parameters.Add("valid", NpgsqlDbType.Text).Value = OpenIddictConstants.Statuses.Valid;
            command.Parameters.Add("ad_hoc", NpgsqlDbType.Text).Value = OpenIddictConstants.AuthorizationTypes.AdHoc;
            command.Parameters.Add("batch_size", NpgsqlDbType.Integer).Value = MartenOpenIddictBulkOperations.BatchSize;

            var count = await session.ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
            result += count;
            if (count < MartenOpenIddictBulkOperations.BatchSize)
            {
                break;
            }
        }

        return result;
    }

    public ValueTask<long> RevokeAsync(
        string? subject,
        string? client,
        string? status,
        string? type,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(client))
        {
            if (!Guid.TryParse(client, out _))
            {
                return ValueTask.FromResult(0L);
            }
        }

        return RevokeAsync(
            session =>
            {
                IQueryable<OpenIddictMartenAuthorization> query = session
                    .Query<OpenIddictMartenAuthorization>();

                if (!string.IsNullOrEmpty(subject))
                {
                    query = query.Where(authorization => authorization.Subject == subject);
                }

                if (!string.IsNullOrEmpty(client))
                {
                    var applicationId = Guid.Parse(client);
                    query = query.Where(authorization => authorization.ApplicationId == applicationId);
                }

                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(authorization => authorization.Status == status);
                }

                if (!string.IsNullOrEmpty(type))
                {
                    query = query.Where(authorization => authorization.Type == type);
                }

                return query;
            },
            cancellationToken);
    }

    public ValueTask<long> RevokeByApplicationIdAsync(
        string identifier,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(identifier);

        return Guid.TryParse(identifier, out var applicationId)
            ? RevokeAsync(
                session => session.Query<OpenIddictMartenAuthorization>()
                    .Where(authorization => authorization.ApplicationId == applicationId),
                cancellationToken)
            : ValueTask.FromResult(0L);
    }

    public ValueTask<long> RevokeBySubjectAsync(
        string subject,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(subject);

        return RevokeAsync(
            session => session.Query<OpenIddictMartenAuthorization>()
                .Where(authorization => authorization.Subject == subject),
            cancellationToken);
    }

    public ValueTask SetApplicationIdAsync(
        OpenIddictMartenAuthorization authorization,
        string? identifier,
        CancellationToken cancellationToken)
        => SetValue<Guid?>(
            authorization,
            string.IsNullOrEmpty(identifier) ? null : Guid.Parse(identifier),
            static (value, item) => value.ApplicationId = item);

    public ValueTask SetCreationDateAsync(
        OpenIddictMartenAuthorization authorization,
        DateTimeOffset? date,
        CancellationToken cancellationToken)
        => SetValue(authorization, date, static (value, item) => value.CreationDate = item);

    public ValueTask SetPropertiesAsync(
        OpenIddictMartenAuthorization authorization,
        ImmutableDictionary<string, JsonElement> properties,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        authorization.Properties = properties
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value.Clone(),
                StringComparer.Ordinal);
        return ValueTask.CompletedTask;
    }

    public ValueTask SetScopesAsync(
        OpenIddictMartenAuthorization authorization,
        ImmutableArray<string> scopes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        authorization.Scopes = scopes.IsDefaultOrEmpty ? [] : scopes.ToArray();
        return ValueTask.CompletedTask;
    }

    public ValueTask SetStatusAsync(
        OpenIddictMartenAuthorization authorization,
        string? status,
        CancellationToken cancellationToken)
        => SetValue(authorization, status, static (value, item) => value.Status = item);

    public ValueTask SetSubjectAsync(
        OpenIddictMartenAuthorization authorization,
        string? subject,
        CancellationToken cancellationToken)
        => SetValue(authorization, subject, static (value, item) => value.Subject = item);

    public ValueTask SetTypeAsync(
        OpenIddictMartenAuthorization authorization,
        string? type,
        CancellationToken cancellationToken)
        => SetValue(authorization, type, static (value, item) => value.Type = item);

    public async ValueTask UpdateAsync(
        OpenIddictMartenAuthorization authorization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        try
        {
            await using var session = _store.LightweightSession();
            MartenOpenIddictRevisionedUpdate.Queue(
                session,
                authorization,
                checked(authorization.Version + 1));
            await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            OpenIddictMartenExceptionHelper.IsConcurrencyException(exception))
        {
            throw OpenIddictMartenExceptionHelper.CreateConcurrencyException(
                "authorization",
                authorization.Id,
                exception);
        }
    }

    private async ValueTask<long> RevokeAsync(
        Func<IDocumentSession, IQueryable<OpenIddictMartenAuthorization>> buildQuery,
        CancellationToken cancellationToken)
    {
        await using var session = _store.LightweightSession();
        var query = buildQuery(session);
        return await MartenOpenIddictBulkOperations.RevokeAsync<OpenIddictMartenAuthorization>(
            session,
            query.OrderBy(authorization => authorization.Id)
                .Select(authorization => new MartenOpenIddictBulkTarget(
                    authorization.Id,
                    authorization.Version)),
            static authorization => authorization.Id,
            static authorization => authorization.Version,
            static authorization => authorization.Status = OpenIddictConstants.Statuses.Revoked,
            "authorization",
            cancellationToken).ConfigureAwait(false);
    }

    private DocumentMapping GetSingleTenantedMapping<TDocument>(string entityName)
    {
        var mapping = _store.Options.FindOrResolveDocumentType(typeof(TDocument)) as DocumentMapping
            ?? throw new InvalidOperationException(
                $"The Marten mapping for '{typeof(TDocument).FullName}' is not a root document mapping.");
        if (mapping.TenancyStyle != TenancyStyle.Single)
        {
            throw new InvalidOperationException(
                $"The Marten OpenIddict {entityName} store only supports single-tenanted documents.");
        }

        return mapping;
    }

    private static string GetIndexedColumn(DocumentMapping mapping, string indexName)
    {
        var index = mapping.Indexes.Single(candidate =>
            string.Equals(candidate.Name, indexName, StringComparison.Ordinal));
        var columns = index.Columns ?? throw new InvalidOperationException(
            $"The Marten index '{indexName}' does not expose a physical column.");
        if (columns.Length != 1)
        {
            throw new InvalidOperationException(
                $"The Marten index '{indexName}' must expose exactly one physical column.");
        }

        var column = columns[0];
        return QuoteIdentifier(column);
    }

    private static string QuoteIdentifier(string identifier)
        => $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static ValueTask<TResult?> GetValue<TResult>(
        OpenIddictMartenAuthorization authorization,
        Func<OpenIddictMartenAuthorization, TResult?> getter)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        return ValueTask.FromResult(getter(authorization));
    }

    private static ValueTask SetValue<TResult>(
        OpenIddictMartenAuthorization authorization,
        TResult value,
        Action<OpenIddictMartenAuthorization, TResult> setter)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        setter(authorization, value);
        return ValueTask.CompletedTask;
    }

    private static async IAsyncEnumerable<OpenIddictMartenAuthorization> FilterAsync(
        IAsyncEnumerable<OpenIddictMartenAuthorization> authorizations,
        Func<OpenIddictMartenAuthorization, bool> predicate,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var authorization in authorizations.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (predicate(authorization))
            {
                yield return authorization;
            }
        }
    }

    private static async IAsyncEnumerable<OpenIddictMartenAuthorization> EmptyAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.CompletedTask.ConfigureAwait(false);
        yield break;
    }
}
