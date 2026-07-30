using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using JasperFx.MultiTenancy;
using Marten;
using Marten.Linq;
using Marten.Schema;
using Npgsql;
using NpgsqlTypes;
using OpenIddict.Abstractions;

namespace Innovorium.OpenIddict.Marten;

internal sealed class MartenOpenIddictTokenStore(
    IDocumentStore store,
    TimeProvider timeProvider) : IOpenIddictTokenStore<OpenIddictMartenToken>
{
    private readonly IDocumentStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public async ValueTask<long> CountAsync(CancellationToken cancellationToken)
    {
        await using var session = _store.LightweightSession();
        return await session.Query<OpenIddictMartenToken>()
            .LongCountAsync(cancellationToken)
            .ConfigureAwait(false);
    }

#pragma warning disable CS8714 // OpenIddict leaves TResult unconstrained; Marten's async LINQ API adds notnull.
    public async ValueTask<long> CountAsync<TResult>(
        Func<IQueryable<OpenIddictMartenToken>, IQueryable<TResult>> query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        await using var session = _store.LightweightSession();
        return await query(session.Query<OpenIddictMartenToken>())
            .LongCountAsync(cancellationToken)
            .ConfigureAwait(false);
    }
#pragma warning restore CS8714

    public async ValueTask CreateAsync(
        OpenIddictMartenToken token,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        await using var session = _store.LightweightSession();
        session.Insert(token);
        await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DeleteAsync(
        OpenIddictMartenToken token,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        var mapping = GetSingleTenantedMapping<OpenIddictMartenToken>("token");
        using var command = new NpgsqlCommand(
            $"delete from {mapping.TableName.QualifiedName} where id = @id and mt_version = @version");
        command.Parameters.Add("id", NpgsqlDbType.Uuid).Value = token.Id;
        command.Parameters.Add("version", NpgsqlDbType.Integer).Value = token.Version;

        await using var session = _store.LightweightSession();
        try
        {
            var affectedRows = await session.ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
            if (affectedRows != 1)
            {
                throw new JasperFx.ConcurrencyException(typeof(OpenIddictMartenToken), token.Id);
            }
        }
        catch (Exception exception) when (
            OpenIddictMartenExceptionHelper.IsConcurrencyException(exception))
        {
            throw OpenIddictMartenExceptionHelper.CreateConcurrencyException(
                "token",
                token.Id,
                exception);
        }
    }

    public IAsyncEnumerable<OpenIddictMartenToken> FindAsync(
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
                return EmptyAsync(cancellationToken);
            }
        }

        return MartenOpenIddictSession.QueryAsync(
            _store,
            (session, cancellation) =>
            {
                IQueryable<OpenIddictMartenToken> query = session.Query<OpenIddictMartenToken>();

                if (!string.IsNullOrEmpty(subject))
                {
                    query = query.Where(token => token.Subject == subject);
                }

                if (!string.IsNullOrEmpty(client))
                {
                    var applicationId = Guid.Parse(client);
                    query = query.Where(token => token.ApplicationId == applicationId);
                }

                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(token => token.Status == status);
                }

                if (!string.IsNullOrEmpty(type))
                {
                    query = query.Where(token => token.Type == type);
                }

                return query.ToAsyncEnumerable(cancellation);
            },
            cancellationToken);
    }

    public IAsyncEnumerable<OpenIddictMartenToken> FindByApplicationIdAsync(
        string identifier,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(identifier);

        return Guid.TryParse(identifier, out var applicationId)
            ? MartenOpenIddictSession.QueryAsync(
                _store,
                (session, cancellation) => session.Query<OpenIddictMartenToken>()
                    .Where(token => token.ApplicationId == applicationId)
                    .ToAsyncEnumerable(cancellation),
                cancellationToken)
            : EmptyAsync(cancellationToken);
    }

    public IAsyncEnumerable<OpenIddictMartenToken> FindByAuthorizationIdAsync(
        string identifier,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(identifier);

        return Guid.TryParse(identifier, out var authorizationId)
            ? MartenOpenIddictSession.QueryAsync(
                _store,
                (session, cancellation) => session.Query<OpenIddictMartenToken>()
                    .Where(token => token.AuthorizationId == authorizationId)
                    .ToAsyncEnumerable(cancellation),
                cancellationToken)
            : EmptyAsync(cancellationToken);
    }

    public async ValueTask<OpenIddictMartenToken?> FindByIdAsync(
        string identifier,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(identifier);

        if (!Guid.TryParse(identifier, out var id))
        {
            return null;
        }

        await using var session = _store.LightweightSession();
        return await session.LoadAsync<OpenIddictMartenToken>(id, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<OpenIddictMartenToken?> FindByReferenceIdAsync(
        string identifier,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(identifier);

        await using var session = _store.LightweightSession();
        await foreach (var token in session.Query<OpenIddictMartenToken>()
            .Where(token => token.ReferenceId == identifier)
            .ToAsyncEnumerable(cancellationToken)
            .ConfigureAwait(false))
        {
            if (string.Equals(token.ReferenceId, identifier, StringComparison.Ordinal))
            {
                return token;
            }
        }

        return null;
    }

    public IAsyncEnumerable<OpenIddictMartenToken> FindBySubjectAsync(
        string subject,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(subject);

        return MartenOpenIddictSession.QueryAsync(
            _store,
            (session, cancellation) => session.Query<OpenIddictMartenToken>()
                .Where(token => token.Subject == subject)
                .ToAsyncEnumerable(cancellation),
            cancellationToken);
    }

    public ValueTask<string?> GetApplicationIdAsync(
        OpenIddictMartenToken token,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        return ValueTask.FromResult(token.ApplicationId?.ToString("D", CultureInfo.InvariantCulture));
    }

#pragma warning disable CS8714 // OpenIddict leaves TResult unconstrained; Marten's async LINQ API adds notnull.
    public async ValueTask<TResult?> GetAsync<TState, TResult>(
        Func<IQueryable<OpenIddictMartenToken>, TState, IQueryable<TResult>> query,
        TState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        await using var session = _store.LightweightSession();
        return await query(session.Query<OpenIddictMartenToken>(), state)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }
#pragma warning restore CS8714

    public ValueTask<string?> GetAuthorizationIdAsync(
        OpenIddictMartenToken token,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        return ValueTask.FromResult(token.AuthorizationId?.ToString("D", CultureInfo.InvariantCulture));
    }

    public ValueTask<DateTimeOffset?> GetCreationDateAsync(
        OpenIddictMartenToken token,
        CancellationToken cancellationToken)
        => GetValue(token, static value => value.CreationDate);

    public ValueTask<DateTimeOffset?> GetExpirationDateAsync(
        OpenIddictMartenToken token,
        CancellationToken cancellationToken)
        => GetValue(token, static value => value.ExpirationDate);

    public ValueTask<string?> GetIdAsync(
        OpenIddictMartenToken token,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        return ValueTask.FromResult<string?>(token.Id.ToString("D", CultureInfo.InvariantCulture));
    }

    public ValueTask<string?> GetPayloadAsync(
        OpenIddictMartenToken token,
        CancellationToken cancellationToken)
        => GetValue(token, static value => value.Payload);

    public ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(
        OpenIddictMartenToken token,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        return ValueTask.FromResult((token.Properties ?? [])
            .ToImmutableDictionary(
                pair => pair.Key,
                pair => pair.Value.Clone(),
                StringComparer.Ordinal));
    }

    public ValueTask<DateTimeOffset?> GetRedemptionDateAsync(
        OpenIddictMartenToken token,
        CancellationToken cancellationToken)
        => GetValue(token, static value => value.RedemptionDate);

    public ValueTask<string?> GetReferenceIdAsync(
        OpenIddictMartenToken token,
        CancellationToken cancellationToken)
        => GetValue(token, static value => value.ReferenceId);

    public ValueTask<string?> GetStatusAsync(
        OpenIddictMartenToken token,
        CancellationToken cancellationToken)
        => GetValue(token, static value => value.Status);

    public ValueTask<string?> GetSubjectAsync(
        OpenIddictMartenToken token,
        CancellationToken cancellationToken)
        => GetValue(token, static value => value.Subject);

    public ValueTask<string?> GetTypeAsync(
        OpenIddictMartenToken token,
        CancellationToken cancellationToken)
        => GetValue(token, static value => value.Type);

    public ValueTask<OpenIddictMartenToken> InstantiateAsync(CancellationToken cancellationToken)
        => ValueTask.FromResult(new OpenIddictMartenToken());

    public IAsyncEnumerable<OpenIddictMartenToken> ListAsync(
        int? count,
        int? offset,
        CancellationToken cancellationToken)
    {
        return MartenOpenIddictSession.QueryAsync(
            _store,
            (session, cancellation) =>
            {
                IQueryable<OpenIddictMartenToken> query = session
                    .Query<OpenIddictMartenToken>()
                    .OrderBy(token => token.Id);

                if (offset is not null)
                {
                    query = query.Skip(offset.Value);
                }

                if (count is not null)
                {
                    query = query.Take(count.Value);
                }

                return query.ToAsyncEnumerable(cancellation);
            },
            cancellationToken);
    }

#pragma warning disable CS8714 // OpenIddict leaves TResult unconstrained; Marten's async LINQ API adds notnull.
    public IAsyncEnumerable<TResult> ListAsync<TState, TResult>(
        Func<IQueryable<OpenIddictMartenToken>, TState, IQueryable<TResult>> query,
        TState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return MartenOpenIddictSession.QueryAsync(
            _store,
            (session, cancellation) => query(session.Query<OpenIddictMartenToken>(), state)
                .ToAsyncEnumerable(cancellation),
            cancellationToken);
    }
#pragma warning restore CS8714

    public async ValueTask<long> PruneAsync(
        DateTimeOffset threshold,
        CancellationToken cancellationToken)
    {
        var tokenMapping = GetSingleTenantedMapping<OpenIddictMartenToken>("token");
        var authorizationMapping = GetSingleTenantedMapping<OpenIddictMartenAuthorization>("authorization");
        var creationDate = GetIndexedColumn(tokenMapping, MartenOpenIddictSchema.TokenCreationDateIndex);
        var expirationDate = GetIndexedColumn(tokenMapping, MartenOpenIddictSchema.TokenExpirationDateIndex);
        var status = GetIndexedColumn(tokenMapping, MartenOpenIddictSchema.TokenStatusIndex);
        var authorizationId = GetIndexedColumn(tokenMapping, MartenOpenIddictSchema.TokenAuthorizationIdIndex);
        var authorizationStatus = GetIndexedColumn(
            authorizationMapping,
            MartenOpenIddictSchema.AuthorizationStatusIndex);
        var now = _timeProvider.GetUtcNow();
        var result = 0L;

        await using var session = _store.LightweightSession();
        for (var index = 0; index < MartenOpenIddictBulkOperations.MaximumBatches; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var command = new NpgsqlCommand($$"""
                with candidates as (
                    select t.id
                    from {{tokenMapping.TableName.QualifiedName}} as t
                    left join {{authorizationMapping.TableName.QualifiedName}} as a
                      on a.id = t.{{authorizationId}}
                    where t.{{creationDate}} < @threshold
                      and ((t.{{status}} is distinct from @inactive
                            and t.{{status}} is distinct from @valid)
                           or t.{{expirationDate}} < @now
                           or (t.{{authorizationId}} is not null
                               and a.{{authorizationStatus}} is distinct from @valid))
                    order by t.id
                    limit @batch_size
                    for update of t skip locked
                )
                delete from {{tokenMapping.TableName.QualifiedName}} as t
                using candidates
                where t.id = candidates.id
                """);
            command.Parameters.Add("threshold", NpgsqlDbType.TimestampTz).Value = threshold.ToUniversalTime();
            command.Parameters.Add("now", NpgsqlDbType.TimestampTz).Value = now;
            command.Parameters.Add("inactive", NpgsqlDbType.Text).Value = OpenIddictConstants.Statuses.Inactive;
            command.Parameters.Add("valid", NpgsqlDbType.Text).Value = OpenIddictConstants.Statuses.Valid;
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
                IQueryable<OpenIddictMartenToken> query = session.Query<OpenIddictMartenToken>();

                if (!string.IsNullOrEmpty(subject))
                {
                    query = query.Where(token => token.Subject == subject);
                }

                if (!string.IsNullOrEmpty(client))
                {
                    var applicationId = Guid.Parse(client);
                    query = query.Where(token => token.ApplicationId == applicationId);
                }

                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(token => token.Status == status);
                }

                if (!string.IsNullOrEmpty(type))
                {
                    query = query.Where(token => token.Type == type);
                }

                return query;
            },
            cancellationToken);
    }

    public ValueTask<long> RevokeByApplicationIdAsync(
        string identifier,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(identifier);

        return Guid.TryParse(identifier, out var applicationId)
            ? RevokeAsync(
                session => session.Query<OpenIddictMartenToken>()
                    .Where(token => token.ApplicationId == applicationId),
                cancellationToken)
            : ValueTask.FromResult(0L);
    }

    public ValueTask<long> RevokeByAuthorizationIdAsync(
        string identifier,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(identifier);

        return Guid.TryParse(identifier, out var authorizationId)
            ? RevokeAsync(
                session => session.Query<OpenIddictMartenToken>()
                    .Where(token => token.AuthorizationId == authorizationId),
                cancellationToken)
            : ValueTask.FromResult(0L);
    }

    public ValueTask<long> RevokeBySubjectAsync(
        string subject,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(subject);

        return RevokeAsync(
            session => session.Query<OpenIddictMartenToken>()
                .Where(token => token.Subject == subject),
            cancellationToken);
    }

    public ValueTask SetApplicationIdAsync(
        OpenIddictMartenToken token,
        string? identifier,
        CancellationToken cancellationToken)
        => SetValue<Guid?>(
            token,
            string.IsNullOrEmpty(identifier) ? null : Guid.Parse(identifier),
            static (value, item) => value.ApplicationId = item);

    public ValueTask SetAuthorizationIdAsync(
        OpenIddictMartenToken token,
        string? identifier,
        CancellationToken cancellationToken)
        => SetValue<Guid?>(
            token,
            string.IsNullOrEmpty(identifier) ? null : Guid.Parse(identifier),
            static (value, item) => value.AuthorizationId = item);

    public ValueTask SetCreationDateAsync(
        OpenIddictMartenToken token,
        DateTimeOffset? date,
        CancellationToken cancellationToken)
        => SetValue(token, date, static (value, item) => value.CreationDate = item);

    public ValueTask SetExpirationDateAsync(
        OpenIddictMartenToken token,
        DateTimeOffset? date,
        CancellationToken cancellationToken)
        => SetValue(token, date, static (value, item) => value.ExpirationDate = item);

    public ValueTask SetPayloadAsync(
        OpenIddictMartenToken token,
        string? payload,
        CancellationToken cancellationToken)
        => SetValue(token, payload, static (value, item) => value.Payload = item);

    public ValueTask SetPropertiesAsync(
        OpenIddictMartenToken token,
        ImmutableDictionary<string, JsonElement> properties,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        token.Properties = properties
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value.Clone(),
                StringComparer.Ordinal);
        return ValueTask.CompletedTask;
    }

    public ValueTask SetRedemptionDateAsync(
        OpenIddictMartenToken token,
        DateTimeOffset? date,
        CancellationToken cancellationToken)
        => SetValue(token, date, static (value, item) => value.RedemptionDate = item);

    public ValueTask SetReferenceIdAsync(
        OpenIddictMartenToken token,
        string? identifier,
        CancellationToken cancellationToken)
        => SetValue(token, identifier, static (value, item) => value.ReferenceId = item);

    public ValueTask SetStatusAsync(
        OpenIddictMartenToken token,
        string? status,
        CancellationToken cancellationToken)
        => SetValue(token, status, static (value, item) => value.Status = item);

    public ValueTask SetSubjectAsync(
        OpenIddictMartenToken token,
        string? subject,
        CancellationToken cancellationToken)
        => SetValue(token, subject, static (value, item) => value.Subject = item);

    public ValueTask SetTypeAsync(
        OpenIddictMartenToken token,
        string? type,
        CancellationToken cancellationToken)
        => SetValue(token, type, static (value, item) => value.Type = item);

    public async ValueTask UpdateAsync(
        OpenIddictMartenToken token,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        try
        {
            await using var session = _store.LightweightSession();
            MartenOpenIddictRevisionedUpdate.Queue(
                session,
                token,
                checked(token.Version + 1));
            await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            OpenIddictMartenExceptionHelper.IsConcurrencyException(exception))
        {
            throw OpenIddictMartenExceptionHelper.CreateConcurrencyException(
                "token",
                token.Id,
                exception);
        }
    }

    private async ValueTask<long> RevokeAsync(
        Func<IDocumentSession, IQueryable<OpenIddictMartenToken>> buildQuery,
        CancellationToken cancellationToken)
    {
        await using var session = _store.LightweightSession();
        var query = buildQuery(session);
        return await MartenOpenIddictBulkOperations.RevokeAsync<OpenIddictMartenToken>(
            session,
            query.OrderBy(token => token.Id)
                .Select(token => new MartenOpenIddictBulkTarget(token.Id, token.Version)),
            static token => token.Id,
            static token => token.Version,
            static token => token.Status = OpenIddictConstants.Statuses.Revoked,
            "token",
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

        return QuoteIdentifier(columns[0]);
    }

    private static string QuoteIdentifier(string identifier)
        => $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static ValueTask<TResult?> GetValue<TResult>(
        OpenIddictMartenToken token,
        Func<OpenIddictMartenToken, TResult?> getter)
    {
        ArgumentNullException.ThrowIfNull(token);

        return ValueTask.FromResult(getter(token));
    }

    private static ValueTask SetValue<TResult>(
        OpenIddictMartenToken token,
        TResult value,
        Action<OpenIddictMartenToken, TResult> setter)
    {
        ArgumentNullException.ThrowIfNull(token);

        setter(token, value);
        return ValueTask.CompletedTask;
    }

    private static async IAsyncEnumerable<OpenIddictMartenToken> EmptyAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.CompletedTask.ConfigureAwait(false);
        yield break;
    }
}
