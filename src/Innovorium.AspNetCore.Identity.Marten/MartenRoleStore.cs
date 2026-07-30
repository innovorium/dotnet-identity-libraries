using System.Security.Claims;
using JasperFx.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Identity;

namespace Innovorium.AspNetCore.Identity.Marten;

/// <summary>
/// Stores ASP.NET Core Identity roles and role claims in a package-owned Marten session.
/// </summary>
/// <typeparam name="TRole">The application role document type.</typeparam>
public sealed class MartenRoleStore<TRole> :
    IRoleStore<TRole>,
    IQueryableRoleStore<TRole>,
    IRoleClaimStore<TRole>
    where TRole : MartenIdentityRole
{
    private readonly IDocumentStore _documentStore;
    private readonly IdentityErrorDescriber _errorDescriber;
    private readonly MartenIdentityPendingChanges<TRole> _pendingChanges = new();
    private IDocumentSession _session;
    private bool _disposed;

    /// <summary>
    /// Initializes a role store backed by a dedicated lightweight Marten session.
    /// </summary>
    public MartenRoleStore(IDocumentStore documentStore, IdentityErrorDescriber errorDescriber)
    {
        ArgumentNullException.ThrowIfNull(documentStore);
        ArgumentNullException.ThrowIfNull(errorDescriber);
        var mapping = documentStore.Options.FindOrResolveDocumentType(typeof(TRole));
        if (mapping.TenancyStyle == TenancyStyle.Conjoined)
        {
            throw new NotSupportedException(
                "Innovorium.AspNetCore.Identity.Marten currently supports single-tenanted role documents only. " +
                "Configure the Identity role mapping with SingleTenanted().");
        }

        _documentStore = documentStore;
        _errorDescriber = errorDescriber;
        _session = documentStore.LightweightSession();
    }

    /// <inheritdoc />
    public IQueryable<TRole> Roles
    {
        get
        {
            ThrowIfDisposed();
            return _session.Query<TRole>();
        }
    }

    /// <inheritdoc />
    public async Task<IdentityResult> CreateAsync(TRole role, CancellationToken cancellationToken)
    {
        ValidateRoleOperation(role, cancellationToken);
        _pendingChanges.Clear();
        _session.Insert(role);
        return await SaveAsync(role, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IdentityResult> UpdateAsync(TRole role, CancellationToken cancellationToken)
    {
        ValidateRoleOperation(role, cancellationToken);
        try
        {
            _pendingChanges.Materialize(_session, role);
            role.ConcurrencyStamp = Guid.NewGuid().ToString();
            _session.Update(role);
        }
        catch
        {
            ResetSession();
            throw;
        }

        return await SaveAsync(role, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IdentityResult> DeleteAsync(TRole role, CancellationToken cancellationToken)
    {
        ValidateRoleOperation(role, cancellationToken);
        _pendingChanges.Clear();
        var mapping = _documentStore.Options.FindOrResolveDocumentType(typeof(TRole));
        var claims = _documentStore.Options.FindOrResolveDocumentType(typeof(MartenIdentityRoleClaim<TRole>));
        var memberships = _documentStore.Options.FindOrResolveDocumentType(typeof(MartenIdentityUserRole));
        var commandText = $"with deleted_role as (delete from {mapping.TableName.QualifiedName} " +
                          $"where id = ? and {mapping.Metadata.Version.Name} = ? returning id, data), " +
                          $"deleted_claims as (delete from {claims.TableName.QualifiedName} " +
                          $"where {MartenIdentitySchema.RoleIdColumn} in (select id from deleted_role)), " +
                          $"deleted_memberships as (delete from {memberships.TableName.QualifiedName} " +
                          $"where {MartenIdentitySchema.RoleIdColumn} in (select id from deleted_role)) " +
                          "select data from deleted_role";

        try
        {
            var deleted = await _session.QueryAsync<TRole>(
                    commandText,
                    cancellationToken,
                    role.Id,
                    role.Version)
                .ConfigureAwait(false);
            if (deleted.Count != 1)
            {
                return IdentityResult.Failed(_errorDescriber.ConcurrencyFailure());
            }

            _session.Eject(role);
            return IdentityResult.Success;
        }
        catch
        {
            ResetSession();
            throw;
        }
    }

    /// <inheritdoc />
    public Task<string> GetRoleIdAsync(TRole role, CancellationToken cancellationToken)
    {
        ValidateRoleOperation(role, cancellationToken);
        return Task.FromResult(role.Id);
    }

    /// <inheritdoc />
    public Task<string?> GetRoleNameAsync(TRole role, CancellationToken cancellationToken)
    {
        ValidateRoleOperation(role, cancellationToken);
        return Task.FromResult(role.Name);
    }

    /// <inheritdoc />
    public Task SetRoleNameAsync(TRole role, string? roleName, CancellationToken cancellationToken)
    {
        ValidateRoleOperation(role, cancellationToken);
        role.Name = roleName;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<string?> GetNormalizedRoleNameAsync(TRole role, CancellationToken cancellationToken)
    {
        ValidateRoleOperation(role, cancellationToken);
        return Task.FromResult(role.NormalizedName);
    }

    /// <inheritdoc />
    public Task SetNormalizedRoleNameAsync(
        TRole role,
        string? normalizedName,
        CancellationToken cancellationToken)
    {
        ValidateRoleOperation(role, cancellationToken);
        role.NormalizedName = normalizedName;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<TRole?> FindByIdAsync(string roleId, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(roleId);
        return _session.LoadAsync<TRole>(roleId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<TRole?> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedRoleName);
        return _session.Query<TRole>()
            .SingleOrDefaultAsync(role => role.NormalizedName == normalizedRoleName, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IList<Claim>> GetClaimsAsync(TRole role, CancellationToken cancellationToken)
    {
        ValidateRoleOperation(role, cancellationToken);
        var claims = await _session.Query<MartenIdentityRoleClaim<TRole>>()
            .Where(document => document.RoleId == role.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return claims.Select(document => new Claim(document.ClaimType!, document.ClaimValue!)).ToList();
    }

    /// <inheritdoc />
    public Task AddClaimAsync(TRole role, Claim claim, CancellationToken cancellationToken)
    {
        ValidateRoleOperation(role, cancellationToken);
        ArgumentNullException.ThrowIfNull(claim);
        var document = new MartenIdentityRoleClaim<TRole>
        {
            Id = MartenIdentityDocumentId.RoleClaim(),
            RoleId = role.Id,
            ClaimType = claim.Type,
            ClaimValue = claim.Value,
        };
        _pendingChanges.Begin(role, MartenIdentityPendingChangeKind.Relationship);
        _pendingChanges.Add(session => session.Insert(document));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task RemoveClaimAsync(TRole role, Claim claim, CancellationToken cancellationToken)
    {
        ValidateRoleOperation(role, cancellationToken);
        ArgumentNullException.ThrowIfNull(claim);
        var matches = await _session.Query<MartenIdentityRoleClaim<TRole>>()
            .Where(document =>
                document.RoleId == role.Id &&
                document.ClaimType == claim.Type &&
                document.ClaimValue == claim.Value)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        _pendingChanges.Begin(role, MartenIdentityPendingChangeKind.Relationship);
        foreach (var match in matches)
        {
            _pendingChanges.Add(session => session.Delete(match));
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _session.Dispose();
        _pendingChanges.Clear();
        _disposed = true;
    }

    private async Task<IdentityResult> SaveAsync(TRole role, CancellationToken cancellationToken)
    {
        try
        {
            await _session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _pendingChanges.Clear();
            return IdentityResult.Success;
        }
        catch (Exception exception) when (MartenIdentityPersistenceErrors.IsConcurrencyFailure(exception))
        {
            ResetSession();
            return IdentityResult.Failed(_errorDescriber.ConcurrencyFailure());
        }
        catch (Exception exception) when (
            MartenIdentityPersistenceErrors.FindUniqueConstraint(exception) ==
            MartenIdentitySchema.NormalizedRoleNameIndex)
        {
            ResetSession();
            return IdentityResult.Failed(_errorDescriber.DuplicateRoleName(role.Name ?? string.Empty));
        }
        catch
        {
            ResetSession();
            throw;
        }
    }

    private void ValidateRoleOperation(TRole role, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(role);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
    }

    private void ResetSession()
    {
        _pendingChanges.Clear();
        _session.Dispose();
        _session = _documentStore.LightweightSession();
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
