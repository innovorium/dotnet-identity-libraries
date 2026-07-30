using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using JasperFx.MultiTenancy;
using Marten;
using Marten.Linq;
using Npgsql;
using NpgsqlTypes;
using OpenIddict.Abstractions;

namespace Innovorium.OpenIddict.Marten;

internal sealed class MartenOpenIddictScopeStore(
    IDocumentSession session) : IOpenIddictScopeStore<OpenIddictMartenScope>
{
    private readonly IDocumentSession _session = session ?? throw new ArgumentNullException(nameof(session));

    public async ValueTask<long> CountAsync(CancellationToken cancellationToken)
        => await _session.Query<OpenIddictMartenScope>().LongCountAsync(cancellationToken).ConfigureAwait(false);

#pragma warning disable CS8714 // OpenIddict leaves TResult unconstrained; Marten's async LINQ API adds notnull.
    public async ValueTask<long> CountAsync<TResult>(
        Func<IQueryable<OpenIddictMartenScope>, IQueryable<TResult>> query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return await query(_session.Query<OpenIddictMartenScope>())
            .LongCountAsync(cancellationToken)
            .ConfigureAwait(false);
    }
#pragma warning restore CS8714

    public async ValueTask CreateAsync(
        OpenIddictMartenScope scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        _session.Insert(scope);
        await _session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DeleteAsync(
        OpenIddictMartenScope scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var mapping = _session.DocumentStore.Options.FindOrResolveDocumentType(
            typeof(OpenIddictMartenScope));

        if (mapping.TenancyStyle != TenancyStyle.Single)
        {
            throw new InvalidOperationException(
                "The Marten OpenIddict scope store only supports single-tenanted documents.");
        }

        using var command = new NpgsqlCommand(
            $"delete from {mapping.TableName.QualifiedName} where id = @id and mt_version = @version");
        command.Parameters.Add("id", NpgsqlDbType.Uuid).Value = scope.Id;
        command.Parameters.Add("version", NpgsqlDbType.Integer).Value = scope.Version;

        try
        {
            var affectedRows = await _session.ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
            if (affectedRows != 1)
            {
                throw new JasperFx.ConcurrencyException(typeof(OpenIddictMartenScope), scope.Id);
            }

            _session.Eject(scope);
        }
        catch (Exception exception) when (
            OpenIddictMartenExceptionHelper.IsConcurrencyException(exception))
        {
            throw OpenIddictMartenExceptionHelper.CreateConcurrencyException(
                "scope",
                scope.Id,
                exception);
        }
    }

    public async ValueTask<OpenIddictMartenScope?> FindByIdAsync(
        string identifier,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(identifier);

        return Guid.TryParse(identifier, out var id)
            ? await _session.LoadAsync<OpenIddictMartenScope>(id, cancellationToken).ConfigureAwait(false)
            : null;
    }

    public async ValueTask<OpenIddictMartenScope?> FindByNameAsync(
        string name,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        await foreach (var scope in _session.Query<OpenIddictMartenScope>()
            .Where(scope => scope.Name == name)
            .ToAsyncEnumerable(cancellationToken)
            .ConfigureAwait(false))
        {
            if (string.Equals(scope.Name, name, StringComparison.Ordinal))
            {
                return scope;
            }
        }

        return null;
    }

    public IAsyncEnumerable<OpenIddictMartenScope> FindByNamesAsync(
        ImmutableArray<string> names,
        CancellationToken cancellationToken)
    {
        if (names.IsDefaultOrEmpty)
        {
            throw new ArgumentException("At least one scope name must be specified.", nameof(names));
        }

        if (names.Any(string.IsNullOrEmpty))
        {
            throw new ArgumentException("Scope names cannot contain null or empty values.", nameof(names));
        }

        var values = names.ToArray();
        var requested = values.ToHashSet(StringComparer.Ordinal);

        return FilterAsync(
            _session.Query<OpenIddictMartenScope>()
                .Where(scope => values.Contains(scope.Name!))
                .ToAsyncEnumerable(cancellationToken),
            scope => scope.Name is not null && requested.Contains(scope.Name),
            cancellationToken);
    }

    public IAsyncEnumerable<OpenIddictMartenScope> FindByResourceAsync(
        string resource,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(resource);

        return FilterAsync(
            _session.Query<OpenIddictMartenScope>()
                .Where(scope => scope.Resources.Contains(resource))
                .ToAsyncEnumerable(cancellationToken),
            scope => scope.Resources.Contains(resource, StringComparer.Ordinal),
            cancellationToken);
    }

#pragma warning disable CS8714 // OpenIddict leaves TResult unconstrained; Marten's async LINQ API adds notnull.
    public async ValueTask<TResult?> GetAsync<TState, TResult>(
        Func<IQueryable<OpenIddictMartenScope>, TState, IQueryable<TResult>> query,
        TState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return await query(_session.Query<OpenIddictMartenScope>(), state)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }
#pragma warning restore CS8714

    public ValueTask<string?> GetDescriptionAsync(
        OpenIddictMartenScope scope,
        CancellationToken cancellationToken)
        => GetValue(scope, static value => value.Description);

    public ValueTask<ImmutableDictionary<CultureInfo, string>> GetDescriptionsAsync(
        OpenIddictMartenScope scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return ValueTask.FromResult((scope.Descriptions ?? [])
            .ToImmutableDictionary(
                pair => CultureInfo.GetCultureInfo(pair.Key),
                pair => pair.Value));
    }

    public ValueTask<string?> GetDisplayNameAsync(
        OpenIddictMartenScope scope,
        CancellationToken cancellationToken)
        => GetValue(scope, static value => value.DisplayName);

    public ValueTask<ImmutableDictionary<CultureInfo, string>> GetDisplayNamesAsync(
        OpenIddictMartenScope scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return ValueTask.FromResult((scope.DisplayNames ?? [])
            .ToImmutableDictionary(
                pair => CultureInfo.GetCultureInfo(pair.Key),
                pair => pair.Value));
    }

    public ValueTask<string?> GetIdAsync(
        OpenIddictMartenScope scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return ValueTask.FromResult<string?>(scope.Id.ToString("D", CultureInfo.InvariantCulture));
    }

    public ValueTask<string?> GetNameAsync(
        OpenIddictMartenScope scope,
        CancellationToken cancellationToken)
        => GetValue(scope, static value => value.Name);

    public ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(
        OpenIddictMartenScope scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return ValueTask.FromResult((scope.Properties ?? [])
            .ToImmutableDictionary(
                pair => pair.Key,
                pair => pair.Value.Clone(),
                StringComparer.Ordinal));
    }

    public ValueTask<ImmutableArray<string>> GetResourcesAsync(
        OpenIddictMartenScope scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return ValueTask.FromResult((scope.Resources ?? []).ToImmutableArray());
    }

    public ValueTask<OpenIddictMartenScope> InstantiateAsync(CancellationToken cancellationToken)
        => ValueTask.FromResult(new OpenIddictMartenScope());

    public IAsyncEnumerable<OpenIddictMartenScope> ListAsync(
        int? count,
        int? offset,
        CancellationToken cancellationToken)
    {
        IQueryable<OpenIddictMartenScope> query = _session
            .Query<OpenIddictMartenScope>()
            .OrderBy(scope => scope.Id);

        if (offset is not null)
        {
            query = query.Skip(offset.Value);
        }

        if (count is not null)
        {
            query = query.Take(count.Value);
        }

        return query.ToAsyncEnumerable(cancellationToken);
    }

#pragma warning disable CS8714 // OpenIddict leaves TResult unconstrained; Marten's async LINQ API adds notnull.
    public IAsyncEnumerable<TResult> ListAsync<TState, TResult>(
        Func<IQueryable<OpenIddictMartenScope>, TState, IQueryable<TResult>> query,
        TState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return query(_session.Query<OpenIddictMartenScope>(), state)
            .ToAsyncEnumerable(cancellationToken);
    }
#pragma warning restore CS8714

    public ValueTask SetDescriptionAsync(
        OpenIddictMartenScope scope,
        string? description,
        CancellationToken cancellationToken)
        => SetValue(scope, description, static (value, item) => value.Description = item);

    public ValueTask SetDescriptionsAsync(
        OpenIddictMartenScope scope,
        ImmutableDictionary<CultureInfo, string> descriptions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(descriptions);

        scope.Descriptions = descriptions.ToDictionary(
            pair => pair.Key.Name,
            pair => pair.Value,
            StringComparer.Ordinal);

        return ValueTask.CompletedTask;
    }

    public ValueTask SetDisplayNameAsync(
        OpenIddictMartenScope scope,
        string? name,
        CancellationToken cancellationToken)
        => SetValue(scope, name, static (value, item) => value.DisplayName = item);

    public ValueTask SetDisplayNamesAsync(
        OpenIddictMartenScope scope,
        ImmutableDictionary<CultureInfo, string> names,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(names);

        scope.DisplayNames = names.ToDictionary(
            pair => pair.Key.Name,
            pair => pair.Value,
            StringComparer.Ordinal);

        return ValueTask.CompletedTask;
    }

    public ValueTask SetNameAsync(
        OpenIddictMartenScope scope,
        string? name,
        CancellationToken cancellationToken)
        => SetValue(scope, name, static (value, item) => value.Name = item);

    public ValueTask SetPropertiesAsync(
        OpenIddictMartenScope scope,
        ImmutableDictionary<string, JsonElement> properties,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(properties);

        scope.Properties = properties.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Clone(),
            StringComparer.Ordinal);
        return ValueTask.CompletedTask;
    }

    public ValueTask SetResourcesAsync(
        OpenIddictMartenScope scope,
        ImmutableArray<string> resources,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        scope.Resources = resources.IsDefaultOrEmpty ? [] : resources.ToArray();
        return ValueTask.CompletedTask;
    }

    public async ValueTask UpdateAsync(
        OpenIddictMartenScope scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        try
        {
            if (!await _session.CheckExistsAsync<OpenIddictMartenScope>(
                scope.Id,
                cancellationToken).ConfigureAwait(false))
            {
                throw new global::Marten.Exceptions.NonExistentDocumentException(
                    typeof(OpenIddictMartenScope),
                    scope.Id);
            }

            _session.UpdateRevision(scope, checked(scope.Version + 1));
            await _session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            OpenIddictMartenExceptionHelper.IsConcurrencyException(exception))
        {
            throw OpenIddictMartenExceptionHelper.CreateConcurrencyException(
                "scope",
                scope.Id,
                exception);
        }
    }

    private static ValueTask<TResult?> GetValue<TResult>(
        OpenIddictMartenScope scope,
        Func<OpenIddictMartenScope, TResult?> getter)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return ValueTask.FromResult(getter(scope));
    }

    private static ValueTask SetValue<TResult>(
        OpenIddictMartenScope scope,
        TResult value,
        Action<OpenIddictMartenScope, TResult> setter)
    {
        ArgumentNullException.ThrowIfNull(scope);
        setter(scope, value);
        return ValueTask.CompletedTask;
    }

    private static async IAsyncEnumerable<OpenIddictMartenScope> FilterAsync(
        IAsyncEnumerable<OpenIddictMartenScope> scopes,
        Func<OpenIddictMartenScope, bool> predicate,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var scope in scopes
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            if (predicate(scope))
            {
                yield return scope;
            }
        }
    }

}
