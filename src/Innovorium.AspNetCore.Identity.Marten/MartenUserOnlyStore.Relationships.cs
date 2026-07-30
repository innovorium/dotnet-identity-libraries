using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Identity;

namespace Innovorium.AspNetCore.Identity.Marten;

public partial class MartenUserOnlyStore<TUser> :
    IUserClaimStore<TUser>,
    IUserLoginStore<TUser>,
    IUserAuthenticationTokenStore<TUser>,
    IUserAuthenticatorKeyStore<TUser>,
    IUserTwoFactorRecoveryCodeStore<TUser>,
    IUserPasskeyStore<TUser>
    where TUser : MartenIdentityUser
{
    private const string InternalLoginProvider = "[AspNetUserStore]";
    private const string AuthenticatorKeyTokenName = "AuthenticatorKey";
    private const string RecoveryCodeTokenName = "RecoveryCodes";

    /// <inheritdoc />
    public async Task<IList<Claim>> GetClaimsAsync(TUser user, CancellationToken cancellationToken)
    {
        ValidateUserOperation(user, cancellationToken);
        var claims = await _session.Query<MartenIdentityUserClaim<TUser>>()
            .Where(document => document.UserId == user.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return claims.Select(document => new Claim(document.ClaimType!, document.ClaimValue!)).ToList();
    }

    /// <inheritdoc />
    public Task AddClaimsAsync(
        TUser user,
        IEnumerable<Claim> claims,
        CancellationToken cancellationToken)
    {
        ValidateUserOperation(user, cancellationToken);
        ArgumentNullException.ThrowIfNull(claims);

        var documents = claims.Select(claim =>
        {
            ArgumentNullException.ThrowIfNull(claim);
            return new MartenIdentityUserClaim<TUser>
            {
                Id = MartenIdentityDocumentId.UserClaim(),
                UserId = user.Id,
                ClaimType = claim.Type,
                ClaimValue = claim.Value,
            };
        }).ToArray();
        BeginPendingChanges(user);
        foreach (var document in documents)
        {
            AddPendingChange(session => session.Insert(document));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task ReplaceClaimAsync(
        TUser user,
        Claim claim,
        Claim newClaim,
        CancellationToken cancellationToken)
    {
        ValidateUserOperation(user, cancellationToken);
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(newClaim);

        var matches = await _session.Query<MartenIdentityUserClaim<TUser>>()
            .Where(document =>
                document.UserId == user.Id &&
                document.ClaimType == claim.Type &&
                document.ClaimValue == claim.Value)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        BeginPendingChanges(user);
        foreach (var match in matches)
        {
            match.ClaimType = newClaim.Type;
            match.ClaimValue = newClaim.Value;
            AddPendingChange(session => session.Store(match));
        }
    }

    /// <inheritdoc />
    public async Task RemoveClaimsAsync(
        TUser user,
        IEnumerable<Claim> claims,
        CancellationToken cancellationToken)
    {
        ValidateUserOperation(user, cancellationToken);
        ArgumentNullException.ThrowIfNull(claims);

        var requestedClaims = claims.ToArray();
        foreach (var claim in requestedClaims)
        {
            ArgumentNullException.ThrowIfNull(claim);
        }

        var matchesToDelete = new List<MartenIdentityUserClaim<TUser>>();
        foreach (var claim in requestedClaims)
        {
            var matches = await _session.Query<MartenIdentityUserClaim<TUser>>()
                .Where(document =>
                    document.UserId == user.Id &&
                    document.ClaimType == claim.Type &&
                    document.ClaimValue == claim.Value)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            matchesToDelete.AddRange(matches);
        }

        BeginPendingChanges(user);
        foreach (var match in matchesToDelete)
        {
            AddPendingChange(session => session.Delete(match));
        }
    }

    /// <inheritdoc />
    public async Task<IList<TUser>> GetUsersForClaimAsync(
        Claim claim,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(claim);

        var userIds = await _session.Query<MartenIdentityUserClaim<TUser>>()
            .Where(document => document.ClaimType == claim.Type && document.ClaimValue == claim.Value)
            .Select(document => document.UserId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (userIds.Count == 0)
        {
            return [];
        }

        var distinctUserIds = userIds.Distinct(StringComparer.Ordinal).ToArray();
        var users = await _session.Query<TUser>()
            .Where(user => distinctUserIds.Contains(user.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return users.ToList();
    }

    /// <inheritdoc />
    public Task AddLoginAsync(
        TUser user,
        UserLoginInfo login,
        CancellationToken cancellationToken)
    {
        ValidateUserOperation(user, cancellationToken);
        ArgumentNullException.ThrowIfNull(login);
        var document = new MartenIdentityUserLogin<TUser>
        {
            Id = MartenIdentityDocumentId.UserLogin(login.LoginProvider, login.ProviderKey),
            UserId = user.Id,
            LoginProvider = login.LoginProvider,
            ProviderKey = login.ProviderKey,
            ProviderDisplayName = login.ProviderDisplayName,
        };
        BeginPendingChanges(user);
        AddPendingChange(session => session.Insert(document));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task RemoveLoginAsync(
        TUser user,
        string loginProvider,
        string providerKey,
        CancellationToken cancellationToken)
    {
        ValidateUserOperation(user, cancellationToken);
        ArgumentNullException.ThrowIfNull(loginProvider);
        ArgumentNullException.ThrowIfNull(providerKey);
        var login = await FindLoginAsync(loginProvider, providerKey, cancellationToken).ConfigureAwait(false);
        BeginPendingChanges(user);
        if (login?.UserId == user.Id)
        {
            AddPendingChange(session => session.Delete(login));
        }
    }

    /// <inheritdoc />
    public async Task<IList<UserLoginInfo>> GetLoginsAsync(
        TUser user,
        CancellationToken cancellationToken)
    {
        ValidateUserOperation(user, cancellationToken);
        var logins = await _session.Query<MartenIdentityUserLogin<TUser>>()
            .Where(document => document.UserId == user.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return logins.Select(document => new UserLoginInfo(
            document.LoginProvider,
            document.ProviderKey,
            document.ProviderDisplayName)).ToList();
    }

    /// <inheritdoc />
    public async Task<TUser?> FindByLoginAsync(
        string loginProvider,
        string providerKey,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(loginProvider);
        ArgumentNullException.ThrowIfNull(providerKey);
        var login = await FindLoginAsync(loginProvider, providerKey, cancellationToken).ConfigureAwait(false);
        return login is null
            ? null
            : await _session.LoadAsync<TUser>(login.UserId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetTokenAsync(
        TUser user,
        string loginProvider,
        string name,
        string? value,
        CancellationToken cancellationToken)
    {
        ValidateTokenOperation(user, loginProvider, name, cancellationToken);
        var id = MartenIdentityDocumentId.UserToken(user.Id, loginProvider, name);
        var token = await _session.LoadAsync<MartenIdentityUserToken<TUser>>(id, cancellationToken)
            .ConfigureAwait(false);
        token ??= new MartenIdentityUserToken<TUser>
        {
            Id = id,
            UserId = user.Id,
            LoginProvider = loginProvider,
            Name = name,
        };
        token.Value = value;
        BeginPendingChanges(user);
        AddPendingChange(session => session.Store(token));
    }

    /// <inheritdoc />
    public async Task RemoveTokenAsync(
        TUser user,
        string loginProvider,
        string name,
        CancellationToken cancellationToken)
    {
        ValidateTokenOperation(user, loginProvider, name, cancellationToken);
        var token = await _session.LoadAsync<MartenIdentityUserToken<TUser>>(
                MartenIdentityDocumentId.UserToken(user.Id, loginProvider, name),
                cancellationToken)
            .ConfigureAwait(false);
        BeginPendingChanges(user);
        if (token is not null)
        {
            AddPendingChange(session => session.Delete(token));
        }
    }

    /// <inheritdoc />
    public async Task<string?> GetTokenAsync(
        TUser user,
        string loginProvider,
        string name,
        CancellationToken cancellationToken)
    {
        ValidateTokenOperation(user, loginProvider, name, cancellationToken);
        var token = await _session.LoadAsync<MartenIdentityUserToken<TUser>>(
                MartenIdentityDocumentId.UserToken(user.Id, loginProvider, name),
                cancellationToken)
            .ConfigureAwait(false);
        return token?.Value;
    }

    /// <inheritdoc />
    public Task SetAuthenticatorKeyAsync(
        TUser user,
        string key,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        return SetTokenAsync(user, InternalLoginProvider, AuthenticatorKeyTokenName, key, cancellationToken);
    }

    /// <inheritdoc />
    public Task<string?> GetAuthenticatorKeyAsync(TUser user, CancellationToken cancellationToken) =>
        GetTokenAsync(user, InternalLoginProvider, AuthenticatorKeyTokenName, cancellationToken);

    /// <inheritdoc />
    public Task ReplaceCodesAsync(
        TUser user,
        IEnumerable<string> recoveryCodes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recoveryCodes);
        var mergedCodes = string.Join(';', recoveryCodes);
        return SetTokenAsync(user, InternalLoginProvider, RecoveryCodeTokenName, mergedCodes, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> RedeemCodeAsync(
        TUser user,
        string code,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(code);
        var mergedCodes = await GetTokenAsync(
                user,
                InternalLoginProvider,
                RecoveryCodeTokenName,
                cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrEmpty(mergedCodes))
        {
            return false;
        }

        var codes = mergedCodes.Split(';');
        if (!codes.Contains(code, StringComparer.Ordinal))
        {
            return false;
        }

        await ReplaceCodesAsync(
                user,
                codes.Where(candidate => !string.Equals(candidate, code, StringComparison.Ordinal)),
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public async Task<int> CountCodesAsync(TUser user, CancellationToken cancellationToken)
    {
        var mergedCodes = await GetTokenAsync(
                user,
                InternalLoginProvider,
                RecoveryCodeTokenName,
                cancellationToken)
            .ConfigureAwait(false);
        return string.IsNullOrEmpty(mergedCodes) ? 0 : mergedCodes.AsSpan().Count(';') + 1;
    }

    /// <inheritdoc />
    public async Task AddOrUpdatePasskeyAsync(
        TUser user,
        UserPasskeyInfo passkey,
        CancellationToken cancellationToken)
    {
        ValidateUserOperation(user, cancellationToken);
        ArgumentNullException.ThrowIfNull(passkey);
        var id = MartenIdentityDocumentId.UserPasskey(passkey.CredentialId);
        var document = await _session.LoadAsync<MartenIdentityUserPasskey<TUser>>(id, cancellationToken)
            .ConfigureAwait(false);
        if (document is not null && !string.Equals(document.UserId, user.Id, StringComparison.Ordinal))
        {
            RejectPendingChanges(user, MartenIdentityErrors.DuplicatePasskey());
            return;
        }

        var isNew = document is null;
        document ??= new MartenIdentityUserPasskey<TUser>
        {
            Id = id,
            UserId = user.Id,
        };
        document.Update(passkey);
        BeginPendingChanges(user);
        AddPendingChange(session =>
        {
            if (isNew)
            {
                session.Insert(document);
            }
            else
            {
                session.Store(document);
            }
        });
    }

    /// <inheritdoc />
    public async Task<IList<UserPasskeyInfo>> GetPasskeysAsync(
        TUser user,
        CancellationToken cancellationToken)
    {
        ValidateUserOperation(user, cancellationToken);
        var passkeys = await _session.Query<MartenIdentityUserPasskey<TUser>>()
            .Where(document => document.UserId == user.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return passkeys.Select(document => document.ToPasskeyInfo()).ToList();
    }

    /// <inheritdoc />
    public async Task<TUser?> FindByPasskeyIdAsync(
        byte[] credentialId,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(credentialId);
        var passkey = await _session.LoadAsync<MartenIdentityUserPasskey<TUser>>(
                MartenIdentityDocumentId.UserPasskey(credentialId),
                cancellationToken)
            .ConfigureAwait(false);
        return passkey is null
            ? null
            : await _session.LoadAsync<TUser>(passkey.UserId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<UserPasskeyInfo?> FindPasskeyAsync(
        TUser user,
        byte[] credentialId,
        CancellationToken cancellationToken)
    {
        ValidateUserOperation(user, cancellationToken);
        ArgumentNullException.ThrowIfNull(credentialId);
        var passkey = await _session.LoadAsync<MartenIdentityUserPasskey<TUser>>(
                MartenIdentityDocumentId.UserPasskey(credentialId),
                cancellationToken)
            .ConfigureAwait(false);
        return passkey?.UserId == user.Id ? passkey.ToPasskeyInfo() : null;
    }

    /// <inheritdoc />
    public async Task RemovePasskeyAsync(
        TUser user,
        byte[] credentialId,
        CancellationToken cancellationToken)
    {
        ValidateUserOperation(user, cancellationToken);
        ArgumentNullException.ThrowIfNull(credentialId);
        var passkey = await _session.LoadAsync<MartenIdentityUserPasskey<TUser>>(
                MartenIdentityDocumentId.UserPasskey(credentialId),
                cancellationToken)
            .ConfigureAwait(false);
        BeginPendingChanges(user);
        if (passkey?.UserId == user.Id)
        {
            AddPendingChange(session => session.Delete(passkey));
        }
    }

    private Task<MartenIdentityUserLogin<TUser>?> FindLoginAsync(
        string loginProvider,
        string providerKey,
        CancellationToken cancellationToken) =>
        _session.LoadAsync<MartenIdentityUserLogin<TUser>>(
            MartenIdentityDocumentId.UserLogin(loginProvider, providerKey),
            cancellationToken);

    private void ValidateUserOperation(TUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
    }

    private void ValidateTokenOperation(
        TUser user,
        string loginProvider,
        string name,
        CancellationToken cancellationToken)
    {
        ValidateUserOperation(user, cancellationToken);
        ArgumentNullException.ThrowIfNull(loginProvider);
        ArgumentNullException.ThrowIfNull(name);
    }
}
