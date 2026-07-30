using Marten;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Innovorium.AspNetCore.Identity.Marten;

/// <summary>
/// Stores ASP.NET Core Identity users and their role memberships in Marten.
/// </summary>
/// <typeparam name="TUser">The application user document type.</typeparam>
/// <typeparam name="TRole">The application role document type.</typeparam>
public sealed class MartenUserStore<TUser, TRole> :
    MartenUserOnlyStore<TUser>,
    IUserRoleStore<TUser>
    where TUser : MartenIdentityUser
    where TRole : MartenIdentityRole
{
    /// <summary>
    /// Initializes a role-aware user store backed by a dedicated lightweight Marten session.
    /// </summary>
    public MartenUserStore(
        global::Marten.IDocumentStore documentStore,
        IOptions<IdentityOptions> options,
        IdentityErrorDescriber errorDescriber)
        : base(documentStore, options, errorDescriber, rolesEnabled: true)
    {
    }

    /// <inheritdoc />
    public async Task AddToRoleAsync(
        TUser user,
        string roleName,
        CancellationToken cancellationToken)
    {
        ValidateRoleOperation(user, roleName, cancellationToken);
        var role = await FindRoleAsync(roleName, cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            DiscardPendingChanges();
            throw new InvalidOperationException($"Role '{roleName}' does not exist.");
        }

        var membership = new MartenIdentityUserRole
        {
            Id = MartenIdentityDocumentId.UserRole(user.Id, role.Id),
            UserId = user.Id,
            RoleId = role.Id,
        };
        BeginPendingChanges(user, MartenIdentityPendingChangeKind.AddRole);
        AddPendingChange(session => session.Insert(membership));
        RegisterDocumentAlreadyExistsFailure(
            typeof(MartenIdentityUserRole),
            UserAlreadyInRoleError(roleName));
    }

    /// <inheritdoc />
    public async Task RemoveFromRoleAsync(
        TUser user,
        string roleName,
        CancellationToken cancellationToken)
    {
        ValidateRoleOperation(user, roleName, cancellationToken);
        var role = await FindRoleAsync(roleName, cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            DiscardPendingChanges();
            return;
        }

        var membership = await Session.LoadAsync<MartenIdentityUserRole>(
                MartenIdentityDocumentId.UserRole(user.Id, role.Id),
                cancellationToken)
            .ConfigureAwait(false);
        BeginPendingChanges(user, MartenIdentityPendingChangeKind.RemoveRole);
        if (membership is not null)
        {
            AddPendingChange(session => session.Delete(membership));
        }
    }

    /// <inheritdoc />
    public async Task<IList<string>> GetRolesAsync(
        TUser user,
        CancellationToken cancellationToken)
    {
        ValidateUserRoleOperation(user, cancellationToken);
        var roleIds = await Session.Query<MartenIdentityUserRole>()
            .Where(document => document.UserId == user.Id)
            .Select(document => document.RoleId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (roleIds.Count == 0)
        {
            return [];
        }

        var roleNames = await Session.Query<TRole>()
            .Where(role => roleIds.Contains(role.Id))
            .Select(role => role.Name!)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return roleNames.ToList();
    }

    /// <inheritdoc />
    public async Task<bool> IsInRoleAsync(
        TUser user,
        string roleName,
        CancellationToken cancellationToken)
    {
        ValidateRoleOperation(user, roleName, cancellationToken);
        var role = await FindRoleAsync(roleName, cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            if (HasPendingChanges(user, MartenIdentityPendingChangeKind.RemoveRole))
            {
                DiscardPendingChanges();
            }

            return false;
        }

        var isInRole = await Session.LoadAsync<MartenIdentityUserRole>(
                MartenIdentityDocumentId.UserRole(user.Id, role.Id),
                cancellationToken)
            .ConfigureAwait(false) is not null;
        if ((isInRole && HasPendingChanges(user, MartenIdentityPendingChangeKind.AddRole)) ||
            (!isInRole && HasPendingChanges(user, MartenIdentityPendingChangeKind.RemoveRole)))
        {
            DiscardPendingChanges();
        }

        return isInRole;
    }

    /// <inheritdoc />
    public async Task<IList<TUser>> GetUsersInRoleAsync(
        string roleName,
        CancellationToken cancellationToken)
    {
        ThrowIfStoreDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);
        var role = await FindRoleAsync(roleName, cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            return [];
        }

        var userIds = await Session.Query<MartenIdentityUserRole>()
            .Where(document => document.RoleId == role.Id)
            .Select(document => document.UserId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (userIds.Count == 0)
        {
            return [];
        }

        var users = await Session.Query<TUser>()
            .Where(user => userIds.Contains(user.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return users.ToList();
    }

    private Task<TRole?> FindRoleAsync(string normalizedRoleName, CancellationToken cancellationToken) =>
        Session.Query<TRole>()
            .SingleOrDefaultAsync(role => role.NormalizedName == normalizedRoleName, cancellationToken);

    private void ValidateRoleOperation(
        TUser user,
        string normalizedRoleName,
        CancellationToken cancellationToken)
    {
        ValidateUserRoleOperation(user, cancellationToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedRoleName);
    }

    private void ValidateUserRoleOperation(TUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ThrowIfStoreDisposed();
        if (cancellationToken.IsCancellationRequested)
        {
            DiscardPendingChanges();
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
