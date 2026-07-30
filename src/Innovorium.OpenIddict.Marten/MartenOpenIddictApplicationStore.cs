using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using JasperFx.MultiTenancy;
using Marten;
using Marten.Linq;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using NpgsqlTypes;
using OpenIddict.Abstractions;

namespace Innovorium.OpenIddict.Marten;

internal sealed class MartenOpenIddictApplicationStore(
    IDocumentSession session) : IOpenIddictApplicationStore<OpenIddictMartenApplication>
{
    private readonly IDocumentSession _session = session ?? throw new ArgumentNullException(nameof(session));

    public async ValueTask<long> CountAsync(CancellationToken cancellationToken)
        => await _session.Query<OpenIddictMartenApplication>().LongCountAsync(cancellationToken).ConfigureAwait(false);

#pragma warning disable CS8714 // OpenIddict leaves TResult unconstrained; Marten's async LINQ API adds notnull.
    public async ValueTask<long> CountAsync<TResult>(
        Func<IQueryable<OpenIddictMartenApplication>, IQueryable<TResult>> query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return await query(_session.Query<OpenIddictMartenApplication>())
            .LongCountAsync(cancellationToken)
            .ConfigureAwait(false);
    }
#pragma warning restore CS8714

    public async ValueTask CreateAsync(
        OpenIddictMartenApplication application,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);

        _session.Insert(application);
        await _session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DeleteAsync(
        OpenIddictMartenApplication application,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);

        var mapping = _session.DocumentStore.Options.FindOrResolveDocumentType(
            typeof(OpenIddictMartenApplication));

        if (mapping.TenancyStyle != TenancyStyle.Single)
        {
            throw new InvalidOperationException(
                "The Marten OpenIddict application store only supports single-tenanted documents.");
        }

        using var command = new NpgsqlCommand(
            $"delete from {mapping.TableName.QualifiedName} where id = @id and mt_version = @version");
        command.Parameters.Add("id", NpgsqlDbType.Uuid).Value = application.Id;
        command.Parameters.Add("version", NpgsqlDbType.Integer).Value = application.Version;

        try
        {
            var affectedRows = await _session.ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
            if (affectedRows != 1)
            {
                throw new JasperFx.ConcurrencyException(
                    typeof(OpenIddictMartenApplication),
                    application.Id);
            }

            _session.Eject(application);
        }
        catch (Exception exception) when (
            OpenIddictMartenExceptionHelper.IsConcurrencyException(exception))
        {
            throw OpenIddictMartenExceptionHelper.CreateConcurrencyException(
                "application",
                application.Id,
                exception);
        }
    }

    public async ValueTask<OpenIddictMartenApplication?> FindByClientIdAsync(
        string identifier,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(identifier);

        await foreach (var application in _session.Query<OpenIddictMartenApplication>()
            .Where(application => application.ClientId == identifier)
            .ToAsyncEnumerable(cancellationToken)
            .ConfigureAwait(false))
        {
            if (string.Equals(application.ClientId, identifier, StringComparison.Ordinal))
            {
                return application;
            }
        }

        return null;
    }

    public async ValueTask<OpenIddictMartenApplication?> FindByIdAsync(
        string identifier,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(identifier);

        return Guid.TryParse(identifier, out var id)
            ? await _session.LoadAsync<OpenIddictMartenApplication>(id, cancellationToken).ConfigureAwait(false)
            : null;
    }

    public IAsyncEnumerable<OpenIddictMartenApplication> FindByPostLogoutRedirectUriAsync(
        string uri,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(uri);

        return FilterAsync(
            _session.Query<OpenIddictMartenApplication>()
                .Where(application => application.PostLogoutRedirectUris.Contains(uri))
                .ToAsyncEnumerable(cancellationToken),
            application => application.PostLogoutRedirectUris.Contains(uri, StringComparer.Ordinal),
            cancellationToken);
    }

    public IAsyncEnumerable<OpenIddictMartenApplication> FindByRedirectUriAsync(
        string uri,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(uri);

        return FilterAsync(
            _session.Query<OpenIddictMartenApplication>()
                .Where(application => application.RedirectUris.Contains(uri))
                .ToAsyncEnumerable(cancellationToken),
            application => application.RedirectUris.Contains(uri, StringComparer.Ordinal),
            cancellationToken);
    }

    public ValueTask<string?> GetApplicationTypeAsync(
        OpenIddictMartenApplication application,
        CancellationToken cancellationToken)
        => GetValue(application, static value => value.ApplicationType);

#pragma warning disable CS8714 // OpenIddict leaves TResult unconstrained; Marten's async LINQ API adds notnull.
    public async ValueTask<TResult?> GetAsync<TState, TResult>(
        Func<IQueryable<OpenIddictMartenApplication>, TState, IQueryable<TResult>> query,
        TState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return await query(_session.Query<OpenIddictMartenApplication>(), state)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }
#pragma warning restore CS8714

    public ValueTask<string?> GetClientIdAsync(
        OpenIddictMartenApplication application,
        CancellationToken cancellationToken)
        => GetValue(application, static value => value.ClientId);

    public ValueTask<string?> GetClientSecretAsync(
        OpenIddictMartenApplication application,
        CancellationToken cancellationToken)
        => GetValue(application, static value => value.ClientSecret);

    public ValueTask<string?> GetClientTypeAsync(
        OpenIddictMartenApplication application,
        CancellationToken cancellationToken)
        => GetValue(application, static value => value.ClientType);

    public ValueTask<string?> GetConsentTypeAsync(
        OpenIddictMartenApplication application,
        CancellationToken cancellationToken)
        => GetValue(application, static value => value.ConsentType);

    public ValueTask<string?> GetDisplayNameAsync(
        OpenIddictMartenApplication application,
        CancellationToken cancellationToken)
        => GetValue(application, static value => value.DisplayName);

    public ValueTask<ImmutableDictionary<CultureInfo, string>> GetDisplayNamesAsync(
        OpenIddictMartenApplication application,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);

        return ValueTask.FromResult((application.DisplayNames ?? [])
            .ToImmutableDictionary(
                pair => CultureInfo.GetCultureInfo(pair.Key),
                pair => pair.Value));
    }

    public ValueTask<string?> GetIdAsync(
        OpenIddictMartenApplication application,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);

        return ValueTask.FromResult<string?>(application.Id.ToString("D", CultureInfo.InvariantCulture));
    }

    public ValueTask<JsonWebKeySet?> GetJsonWebKeySetAsync(
        OpenIddictMartenApplication application,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);

        return ValueTask.FromResult(string.IsNullOrEmpty(application.JsonWebKeySet)
            ? null
            : JsonWebKeySet.Create(application.JsonWebKeySet));
    }

    public ValueTask<ImmutableArray<string>> GetPermissionsAsync(
        OpenIddictMartenApplication application,
        CancellationToken cancellationToken)
        => GetArray(application, static value => value.Permissions);

    public ValueTask<ImmutableArray<string>> GetPostLogoutRedirectUrisAsync(
        OpenIddictMartenApplication application,
        CancellationToken cancellationToken)
        => GetArray(application, static value => value.PostLogoutRedirectUris);

    public ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(
        OpenIddictMartenApplication application,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);

        return ValueTask.FromResult((application.Properties ?? [])
            .ToImmutableDictionary(
                pair => pair.Key,
                pair => pair.Value.Clone(),
                StringComparer.Ordinal));
    }

    public ValueTask<ImmutableArray<string>> GetRedirectUrisAsync(
        OpenIddictMartenApplication application,
        CancellationToken cancellationToken)
        => GetArray(application, static value => value.RedirectUris);

    public ValueTask<ImmutableArray<string>> GetRequirementsAsync(
        OpenIddictMartenApplication application,
        CancellationToken cancellationToken)
        => GetArray(application, static value => value.Requirements);

    public ValueTask<ImmutableDictionary<string, string>> GetSettingsAsync(
        OpenIddictMartenApplication application,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);

        return ValueTask.FromResult((application.Settings ?? [])
            .ToImmutableDictionary(StringComparer.Ordinal));
    }

    public ValueTask<OpenIddictMartenApplication> InstantiateAsync(CancellationToken cancellationToken)
        => ValueTask.FromResult(new OpenIddictMartenApplication());

    public IAsyncEnumerable<OpenIddictMartenApplication> ListAsync(
        int? count,
        int? offset,
        CancellationToken cancellationToken)
    {
        IQueryable<OpenIddictMartenApplication> query = _session
            .Query<OpenIddictMartenApplication>()
            .OrderBy(application => application.Id);

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
        Func<IQueryable<OpenIddictMartenApplication>, TState, IQueryable<TResult>> query,
        TState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return query(_session.Query<OpenIddictMartenApplication>(), state)
            .ToAsyncEnumerable(cancellationToken);
    }
#pragma warning restore CS8714

    public ValueTask SetApplicationTypeAsync(
        OpenIddictMartenApplication application,
        string? type,
        CancellationToken cancellationToken)
        => SetValue(application, type, static (value, item) => value.ApplicationType = item);

    public ValueTask SetClientIdAsync(
        OpenIddictMartenApplication application,
        string? identifier,
        CancellationToken cancellationToken)
        => SetValue(application, identifier, static (value, item) => value.ClientId = item);

    public ValueTask SetClientSecretAsync(
        OpenIddictMartenApplication application,
        string? secret,
        CancellationToken cancellationToken)
        => SetValue(application, secret, static (value, item) => value.ClientSecret = item);

    public ValueTask SetClientTypeAsync(
        OpenIddictMartenApplication application,
        string? type,
        CancellationToken cancellationToken)
        => SetValue(application, type, static (value, item) => value.ClientType = item);

    public ValueTask SetConsentTypeAsync(
        OpenIddictMartenApplication application,
        string? type,
        CancellationToken cancellationToken)
        => SetValue(application, type, static (value, item) => value.ConsentType = item);

    public ValueTask SetDisplayNameAsync(
        OpenIddictMartenApplication application,
        string? name,
        CancellationToken cancellationToken)
        => SetValue(application, name, static (value, item) => value.DisplayName = item);

    public ValueTask SetDisplayNamesAsync(
        OpenIddictMartenApplication application,
        ImmutableDictionary<CultureInfo, string> names,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(names);

        application.DisplayNames = names.ToDictionary(
            pair => pair.Key.Name,
            pair => pair.Value,
            StringComparer.Ordinal);

        return ValueTask.CompletedTask;
    }

    public ValueTask SetJsonWebKeySetAsync(
        OpenIddictMartenApplication application,
        JsonWebKeySet? set,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);

        application.JsonWebKeySet = set is null ? null : JsonSerializer.Serialize(set);
        return ValueTask.CompletedTask;
    }

    public ValueTask SetPermissionsAsync(
        OpenIddictMartenApplication application,
        ImmutableArray<string> permissions,
        CancellationToken cancellationToken)
        => SetArray(application, permissions, static (value, items) => value.Permissions = items);

    public ValueTask SetPostLogoutRedirectUrisAsync(
        OpenIddictMartenApplication application,
        ImmutableArray<string> uris,
        CancellationToken cancellationToken)
        => SetArray(application, uris, static (value, items) => value.PostLogoutRedirectUris = items);

    public ValueTask SetPropertiesAsync(
        OpenIddictMartenApplication application,
        ImmutableDictionary<string, JsonElement> properties,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(properties);

        application.Properties = properties.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Clone(),
            StringComparer.Ordinal);
        return ValueTask.CompletedTask;
    }

    public ValueTask SetRedirectUrisAsync(
        OpenIddictMartenApplication application,
        ImmutableArray<string> uris,
        CancellationToken cancellationToken)
        => SetArray(application, uris, static (value, items) => value.RedirectUris = items);

    public ValueTask SetRequirementsAsync(
        OpenIddictMartenApplication application,
        ImmutableArray<string> requirements,
        CancellationToken cancellationToken)
        => SetArray(application, requirements, static (value, items) => value.Requirements = items);

    public ValueTask SetSettingsAsync(
        OpenIddictMartenApplication application,
        ImmutableDictionary<string, string> settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(settings);

        application.Settings = settings.ToDictionary(StringComparer.Ordinal);
        return ValueTask.CompletedTask;
    }

    public async ValueTask UpdateAsync(
        OpenIddictMartenApplication application,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);

        try
        {
            if (!await _session.CheckExistsAsync<OpenIddictMartenApplication>(
                application.Id,
                cancellationToken).ConfigureAwait(false))
            {
                throw new global::Marten.Exceptions.NonExistentDocumentException(
                    typeof(OpenIddictMartenApplication),
                    application.Id);
            }

            _session.UpdateRevision(application, checked(application.Version + 1));
            await _session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            OpenIddictMartenExceptionHelper.IsConcurrencyException(exception))
        {
            throw OpenIddictMartenExceptionHelper.CreateConcurrencyException(
                "application",
                application.Id,
                exception);
        }
    }

    private static ValueTask<TResult?> GetValue<TResult>(
        OpenIddictMartenApplication application,
        Func<OpenIddictMartenApplication, TResult?> getter)
    {
        ArgumentNullException.ThrowIfNull(application);
        return ValueTask.FromResult(getter(application));
    }

    private static ValueTask<ImmutableArray<string>> GetArray(
        OpenIddictMartenApplication application,
        Func<OpenIddictMartenApplication, string[]?> getter)
    {
        ArgumentNullException.ThrowIfNull(application);
        return ValueTask.FromResult((getter(application) ?? []).ToImmutableArray());
    }

    private static ValueTask SetValue<TResult>(
        OpenIddictMartenApplication application,
        TResult value,
        Action<OpenIddictMartenApplication, TResult> setter)
    {
        ArgumentNullException.ThrowIfNull(application);
        setter(application, value);
        return ValueTask.CompletedTask;
    }

    private static ValueTask SetArray(
        OpenIddictMartenApplication application,
        ImmutableArray<string> values,
        Action<OpenIddictMartenApplication, string[]> setter)
    {
        ArgumentNullException.ThrowIfNull(application);
        setter(application, values.IsDefaultOrEmpty ? [] : values.ToArray());
        return ValueTask.CompletedTask;
    }

    private static async IAsyncEnumerable<OpenIddictMartenApplication> FilterAsync(
        IAsyncEnumerable<OpenIddictMartenApplication> applications,
        Func<OpenIddictMartenApplication, bool> predicate,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var application in applications
            .WithCancellation(cancellationToken)
            .ConfigureAwait(false))
        {
            if (predicate(application))
            {
                yield return application;
            }
        }
    }

}
