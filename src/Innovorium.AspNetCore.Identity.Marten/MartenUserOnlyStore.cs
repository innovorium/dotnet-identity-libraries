using JasperFx.MultiTenancy;
using Marten;
using Marten.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Innovorium.AspNetCore.Identity.Marten;

/// <summary>
/// Stores the core ASP.NET Core Identity user account state in a package-owned Marten session.
/// </summary>
/// <typeparam name="TUser">The application user document type.</typeparam>
public partial class MartenUserOnlyStore<TUser> :
    IUserStore<TUser>,
    IQueryableUserStore<TUser>,
    IUserPasswordStore<TUser>,
    IUserEmailStore<TUser>,
    IUserPhoneNumberStore<TUser>,
    IUserSecurityStampStore<TUser>,
    IUserLockoutStore<TUser>,
    IUserTwoFactorStore<TUser>
    where TUser : MartenIdentityUser
{
    private readonly IDocumentStore _documentStore;
    private readonly IdentityErrorDescriber _errorDescriber;
    private readonly MartenIdentityPendingChanges<TUser> _pendingChanges = new();
    private IDocumentSession _session;
    private readonly bool _rolesEnabled;
    private bool _disposed;

    /// <summary>
    /// Initializes a new store backed by a dedicated lightweight Marten session.
    /// </summary>
    /// <param name="documentStore">The host-configured Marten document store.</param>
    /// <param name="options">The active ASP.NET Core Identity options.</param>
    /// <param name="errorDescriber">The Identity error describer used for standard failures.</param>
    public MartenUserOnlyStore(
        IDocumentStore documentStore,
        IOptions<IdentityOptions> options,
        IdentityErrorDescriber errorDescriber)
        : this(documentStore, options, errorDescriber, rolesEnabled: false)
    {
    }

    internal MartenUserOnlyStore(
        IDocumentStore documentStore,
        IOptions<IdentityOptions> options,
        IdentityErrorDescriber errorDescriber,
        bool rolesEnabled)
    {
        ArgumentNullException.ThrowIfNull(documentStore);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(errorDescriber);

        if (options.Value.Stores.ProtectPersonalData)
        {
            throw new NotSupportedException(
                "Innovorium.AspNetCore.Identity.Marten does not yet implement IProtectedUserStore. " +
                "Disable IdentityOptions.Stores.ProtectPersonalData or use a store that supports personal-data protection.");
        }

        var mapping = documentStore.Options.FindOrResolveDocumentType(typeof(TUser));
        if (mapping.TenancyStyle == TenancyStyle.Conjoined)
        {
            throw new NotSupportedException(
                "Innovorium.AspNetCore.Identity.Marten currently supports single-tenanted user documents only. " +
                "Configure the Identity user mapping with SingleTenanted().");
        }

        _documentStore = documentStore;
        _errorDescriber = errorDescriber;
        _rolesEnabled = rolesEnabled;
        _session = documentStore.LightweightSession();
    }

    /// <inheritdoc />
    public IQueryable<TUser> Users
    {
        get
        {
            ThrowIfDisposed();
            return _session.Query<TUser>();
        }
    }

    /// <inheritdoc />
    public async Task<IdentityResult> CreateAsync(TUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        _pendingChanges.Clear();
        _session.Insert(user);
        return await SaveAsync(user, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IdentityResult> UpdateAsync(TUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        if (_pendingChanges.TryTakeFailure(user, out var pendingFailure))
        {
            return IdentityResult.Failed(pendingFailure!);
        }

        try
        {
            _pendingChanges.Materialize(_session, user);
            user.ConcurrencyStamp = Guid.NewGuid().ToString();
            _session.Update(user);
        }
        catch
        {
            ResetSession();
            throw;
        }

        return await SaveAsync(user, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IdentityResult> DeleteAsync(TUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        _pendingChanges.Clear();
        var mapping = _documentStore.Options.FindOrResolveDocumentType(typeof(TUser));
        var relationships = new List<Type>
        {
            typeof(MartenIdentityUserClaim<TUser>),
            typeof(MartenIdentityUserLogin<TUser>),
            typeof(MartenIdentityUserToken<TUser>),
            typeof(MartenIdentityUserPasskey<TUser>),
        };
        if (_rolesEnabled)
        {
            relationships.Add(typeof(MartenIdentityUserRole));
        }

        var cleanupCommands = relationships.Select((type, index) =>
        {
            var relationship = _documentStore.Options.FindOrResolveDocumentType(type);
            return $"deleted_{index} as (delete from {relationship.TableName.QualifiedName} " +
                   $"where {MartenIdentitySchema.UserIdColumn} in (select id from deleted_user))";
        });
        var commandText = $"with deleted_user as (delete from {mapping.TableName.QualifiedName} " +
                          $"where id = ? and {mapping.Metadata.Version.Name} = ? returning id, data), " +
                          string.Join(", ", cleanupCommands) +
                          " select data from deleted_user";
        object[] parameters = [user.Id, user.Version];

        try
        {
            var deleted = await _session.QueryAsync<TUser>(
                commandText,
                cancellationToken,
                parameters).ConfigureAwait(false);

            if (deleted.Count != 1)
            {
                return IdentityResult.Failed(_errorDescriber.ConcurrencyFailure());
            }

            _session.Eject(user);
            return IdentityResult.Success;
        }
        catch
        {
            ResetSession();
            throw;
        }
    }

    /// <inheritdoc />
    public Task<string> GetUserIdAsync(TUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(user.Id);
    }

    /// <inheritdoc />
    public Task<string?> GetUserNameAsync(TUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(user.UserName);
    }

    /// <inheritdoc />
    public Task SetUserNameAsync(TUser user, string? userName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        user.UserName = userName;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<string?> GetNormalizedUserNameAsync(TUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(user.NormalizedUserName);
    }

    /// <inheritdoc />
    public Task SetNormalizedUserNameAsync(TUser user, string? normalizedName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        user.NormalizedUserName = normalizedName;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<TUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ThrowIfDisposed();
        return _session.LoadAsync<TUser>(userId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<TUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedUserName);
        ThrowIfDisposed();
        return _session.Query<TUser>()
            .SingleOrDefaultAsync(user => user.NormalizedUserName == normalizedUserName, cancellationToken);
    }

    /// <inheritdoc />
    public Task SetPasswordHashAsync(TUser user, string? passwordHash, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        user.PasswordHash = passwordHash;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<string?> GetPasswordHashAsync(TUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(user.PasswordHash);
    }

    /// <inheritdoc />
    public Task<bool> HasPasswordAsync(TUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(user.PasswordHash is not null);
    }

    /// <inheritdoc />
    public Task SetEmailAsync(TUser user, string? email, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        user.Email = email;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<string?> GetEmailAsync(TUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(user.Email);
    }

    /// <inheritdoc />
    public Task<bool> GetEmailConfirmedAsync(TUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(user.EmailConfirmed);
    }

    /// <inheritdoc />
    public Task SetEmailConfirmedAsync(TUser user, bool confirmed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        user.EmailConfirmed = confirmed;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<TUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedEmail);
        ThrowIfDisposed();
        return _session.Query<TUser>()
            .SingleOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);
    }

    /// <inheritdoc />
    public Task<string?> GetNormalizedEmailAsync(TUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(user.NormalizedEmail);
    }

    /// <inheritdoc />
    public Task SetNormalizedEmailAsync(TUser user, string? normalizedEmail, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        user.NormalizedEmail = normalizedEmail;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SetPhoneNumberAsync(TUser user, string? phoneNumber, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        user.PhoneNumber = phoneNumber;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<string?> GetPhoneNumberAsync(TUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(user.PhoneNumber);
    }

    /// <inheritdoc />
    public Task<bool> GetPhoneNumberConfirmedAsync(TUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(user.PhoneNumberConfirmed);
    }

    /// <inheritdoc />
    public Task SetPhoneNumberConfirmedAsync(TUser user, bool confirmed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        user.PhoneNumberConfirmed = confirmed;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SetSecurityStampAsync(TUser user, string stamp, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(stamp);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        user.SecurityStamp = stamp;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<string?> GetSecurityStampAsync(TUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(user.SecurityStamp);
    }

    /// <inheritdoc />
    public Task<DateTimeOffset?> GetLockoutEndDateAsync(TUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(user.LockoutEnd);
    }

    /// <inheritdoc />
    public Task SetLockoutEndDateAsync(TUser user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        user.LockoutEnd = lockoutEnd;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<int> IncrementAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        user.AccessFailedCount++;
        return Task.FromResult(user.AccessFailedCount);
    }

    /// <inheritdoc />
    public Task ResetAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        user.AccessFailedCount = 0;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<int> GetAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(user.AccessFailedCount);
    }

    /// <inheritdoc />
    public Task<bool> GetLockoutEnabledAsync(TUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(user.LockoutEnabled);
    }

    /// <inheritdoc />
    public Task SetLockoutEnabledAsync(TUser user, bool enabled, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        user.LockoutEnabled = enabled;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SetTwoFactorEnabledAsync(TUser user, bool enabled, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        user.TwoFactorEnabled = enabled;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> GetTwoFactorEnabledAsync(TUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(user.TwoFactorEnabled);
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
        GC.SuppressFinalize(this);
    }

    private async Task<IdentityResult> SaveAsync(TUser user, CancellationToken cancellationToken)
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
        catch (Exception exception) when (MartenIdentityPersistenceErrors.FindUniqueConstraint(exception) is
            IdentityBuilderExtensions.NormalizedUserNameIndex or
            IdentityBuilderExtensions.UniqueNormalizedEmailIndex)
        {
            ResetSession();
            var constraint = MartenIdentityPersistenceErrors.FindUniqueConstraint(exception);

            return constraint switch
            {
                IdentityBuilderExtensions.NormalizedUserNameIndex =>
                    IdentityResult.Failed(_errorDescriber.DuplicateUserName(user.UserName ?? string.Empty)),
                IdentityBuilderExtensions.UniqueNormalizedEmailIndex =>
                    IdentityResult.Failed(_errorDescriber.DuplicateEmail(user.Email ?? string.Empty)),
                _ => throw new InvalidOperationException("An expected Identity constraint was not found.", exception),
            };
        }
        catch (Exception exception) when (
            MartenIdentityPersistenceErrors.FindDocumentAlreadyExistsType(exception) is { } documentType &&
            _pendingChanges.HasDocumentAlreadyExistsFailure(documentType))
        {
            var failure = _pendingChanges.GetDocumentAlreadyExistsFailure(documentType);
            ResetSession();
            return IdentityResult.Failed(failure);
        }
        catch (Exception exception) when (
            MartenIdentityPersistenceErrors.FindUniqueConstraint(exception) ==
            MartenIdentitySchema.UserPasskeyPrimaryKey)
        {
            ResetSession();
            return IdentityResult.Failed(MartenIdentityErrors.DuplicatePasskey());
        }
        catch (Exception exception) when (
            MartenIdentityPersistenceErrors.FindForeignKeyConstraint(exception) ==
            MartenIdentitySchema.UserRoleRoleForeignKey)
        {
            ResetSession();
            return IdentityResult.Failed(new IdentityError
            {
                Code = "RoleNotFound",
                Description = "The role no longer exists.",
            });
        }
        catch
        {
            ResetSession();
            throw;
        }
    }

    private void ResetSession()
    {
        _pendingChanges.Clear();
        _session.Dispose();
        _session = _documentStore.LightweightSession();
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_disposed, this);

    internal IDocumentSession Session => _session;

    internal void BeginPendingChanges(
        TUser user,
        MartenIdentityPendingChangeKind kind = MartenIdentityPendingChangeKind.Relationship) =>
        _pendingChanges.Begin(user, kind);

    internal void AddPendingChange(Action<IDocumentSession> operation) => _pendingChanges.Add(operation);

    internal void RegisterDocumentAlreadyExistsFailure(Type documentType, IdentityError failure) =>
        _pendingChanges.RegisterDocumentAlreadyExistsFailure(documentType, failure);

    internal void RejectPendingChanges(TUser user, IdentityError failure) =>
        _pendingChanges.Reject(user, failure);

    internal bool HasPendingChanges(TUser user, MartenIdentityPendingChangeKind kind) =>
        _pendingChanges.IsFor(user, kind);

    internal void DiscardPendingChanges() => _pendingChanges.Clear();

    internal IdentityError LoginAlreadyAssociatedError() => _errorDescriber.LoginAlreadyAssociated();

    internal IdentityError UserAlreadyInRoleError(string roleName) => _errorDescriber.UserAlreadyInRole(roleName);

    internal void ThrowIfStoreDisposed() => ThrowIfDisposed();
}
