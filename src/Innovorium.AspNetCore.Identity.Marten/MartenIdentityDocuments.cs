using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;

namespace Innovorium.AspNetCore.Identity.Marten;

internal static class MartenIdentityDocumentId
{
    public static string UserClaim() => Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

    public static string RoleClaim() => Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

    public static string UserLogin(string loginProvider, string providerKey) =>
        Hash("login", loginProvider, providerKey);

    public static string UserToken(string userId, string loginProvider, string name) =>
        Hash("token", userId, loginProvider, name);

    public static string UserRole(string userId, string roleId) =>
        Hash("role", userId, roleId);

    public static string UserPasskey(byte[] credentialId)
    {
        ArgumentNullException.ThrowIfNull(credentialId);
        var scope = Encoding.UTF8.GetBytes("passkey\0");
        var input = new byte[scope.Length + credentialId.Length];
        scope.CopyTo(input, 0);
        credentialId.CopyTo(input, scope.Length);
        return Convert.ToHexStringLower(SHA256.HashData(input));
    }

    private static string Hash(string scope, params string[] values)
    {
        var builder = new StringBuilder(scope).Append('\0');
        foreach (var value in values)
        {
            builder.Append(value.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(value);
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }
}

internal sealed class MartenIdentityUserClaim<TUser>
    where TUser : MartenIdentityUser
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string? ClaimType { get; set; }
    public string? ClaimValue { get; set; }
}

internal sealed class MartenIdentityUserLogin<TUser>
    where TUser : MartenIdentityUser
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string LoginProvider { get; set; } = string.Empty;
    public string ProviderKey { get; set; } = string.Empty;
    public string? ProviderDisplayName { get; set; }
}

internal sealed class MartenIdentityUserToken<TUser>
    where TUser : MartenIdentityUser
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string LoginProvider { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Value { get; set; }
}

internal sealed class MartenIdentityUserPasskey<TUser>
    where TUser : MartenIdentityUser
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public byte[] CredentialId { get; set; } = [];
    public byte[] PublicKey { get; set; } = [];
    public string? Name { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public uint SignCount { get; set; }
    public string[]? Transports { get; set; }
    public bool IsUserVerified { get; set; }
    public bool IsBackupEligible { get; set; }
    public bool IsBackedUp { get; set; }
    public byte[] AttestationObject { get; set; } = [];
    public byte[] ClientDataJson { get; set; } = [];

    public void Update(UserPasskeyInfo passkey)
    {
        CredentialId = [.. passkey.CredentialId];
        PublicKey = [.. passkey.PublicKey];
        Name = passkey.Name;
        CreatedAt = passkey.CreatedAt;
        SignCount = passkey.SignCount;
        Transports = passkey.Transports is null ? null : [.. passkey.Transports];
        IsUserVerified = passkey.IsUserVerified;
        IsBackupEligible = passkey.IsBackupEligible;
        IsBackedUp = passkey.IsBackedUp;
        AttestationObject = [.. passkey.AttestationObject];
        ClientDataJson = [.. passkey.ClientDataJson];
    }

    public UserPasskeyInfo ToPasskeyInfo() =>
        new(
            [.. CredentialId],
            [.. PublicKey],
            CreatedAt,
            SignCount,
            Transports is null ? null : [.. Transports],
            IsUserVerified,
            IsBackupEligible,
            IsBackedUp,
            [.. AttestationObject],
            [.. ClientDataJson])
        {
            Name = Name,
        };
}

internal sealed class MartenIdentityUserRole
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string RoleId { get; set; } = string.Empty;
}

internal sealed class MartenIdentityRoleClaim<TRole>
    where TRole : MartenIdentityRole
{
    public string Id { get; set; } = string.Empty;
    public string RoleId { get; set; } = string.Empty;
    public string? ClaimType { get; set; }
    public string? ClaimValue { get; set; }
}
