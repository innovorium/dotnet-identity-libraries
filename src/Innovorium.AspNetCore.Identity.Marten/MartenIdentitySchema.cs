using System.Linq.Expressions;
using Marten;

namespace Innovorium.AspNetCore.Identity.Marten;

internal static class MartenIdentitySchema
{
    public const string UserDocumentAlias = "identity_user";
    public const string RoleDocumentAlias = "identity_role";
    public const string UserClaimDocumentAlias = "identity_user_claim";
    public const string UserLoginDocumentAlias = "identity_user_login";
    public const string UserTokenDocumentAlias = "identity_user_token";
    public const string UserPasskeyDocumentAlias = "identity_user_passkey";
    public const string UserRoleDocumentAlias = "identity_user_role";
    public const string RoleClaimDocumentAlias = "identity_role_claim";

    public const string NormalizedUserNameIndex = "uidx_identity_user_normalized_username";
    public const string NormalizedEmailIndex = "idx_identity_user_normalized_email";
    public const string UniqueNormalizedEmailIndex = "uidx_identity_user_normalized_email";
    public const string NormalizedRoleNameIndex = "uidx_identity_role_normalized_name";
    public const string UserClaimUserIdIndex = "idx_identity_user_claim_user_id";
    public const string UserClaimValueIndex = "idx_identity_user_claim_value";
    public const string UserLoginUserIdIndex = "idx_identity_user_login_user_id";
    public const string UserTokenUserIdIndex = "idx_identity_user_token_user_id";
    public const string UserPasskeyUserIdIndex = "idx_identity_user_passkey_user_id";
    public const string UserRoleUserIdIndex = "idx_identity_user_role_user_id";
    public const string UserRoleRoleIdIndex = "idx_identity_user_role_role_id";
    public const string UserRoleRoleForeignKey = "mt_doc_identity_user_role_role_id_fkey";
    public const string RoleClaimRoleIdIndex = "idx_identity_role_claim_role_id";

    public const string UserIdColumn = "user_id";
    public const string RoleIdColumn = "role_id";

    public static void ConfigureUser<TUser>(StoreOptions options, bool requireUniqueEmail)
        where TUser : MartenIdentityUser
    {
        var user = options.Schema.For<TUser>();
        user.DocumentAlias(UserDocumentAlias);
        user.UseOptimisticConcurrency(true);
        user.UniqueIndex(NormalizedUserNameIndex, document => document.NormalizedUserName!);

        if (requireUniqueEmail)
        {
            user.UniqueIndex(UniqueNormalizedEmailIndex, document => document.NormalizedEmail!);
        }
        else
        {
            user.Index(document => document.NormalizedEmail!, index => index.Name = NormalizedEmailIndex);
        }

        ConfigureUserClaim<TUser>(options);
        ConfigureUserRelationship<MartenIdentityUserLogin<TUser>>(
            options,
            UserLoginDocumentAlias,
            UserLoginUserIdIndex,
            document => document.UserId);
        ConfigureUserRelationship<MartenIdentityUserToken<TUser>>(
            options,
            UserTokenDocumentAlias,
            UserTokenUserIdIndex,
            document => document.UserId);
        ConfigureUserRelationship<MartenIdentityUserPasskey<TUser>>(
            options,
            UserPasskeyDocumentAlias,
            UserPasskeyUserIdIndex,
            document => document.UserId);
    }

    public static void ConfigureRoles<TUser, TRole>(StoreOptions options)
        where TUser : MartenIdentityUser
        where TRole : MartenIdentityRole
    {
        var role = options.Schema.For<TRole>();
        role.DocumentAlias(RoleDocumentAlias);
        role.UseOptimisticConcurrency(true);
        role.UniqueIndex(NormalizedRoleNameIndex, document => document.NormalizedName!);

        var membership = options.Schema.For<MartenIdentityUserRole>();
        membership.DocumentAlias(UserRoleDocumentAlias);
        membership.Duplicate(
            document => document.UserId,
            null,
            null,
            index => index.Name = UserRoleUserIdIndex,
            true,
            false);
        membership.ForeignKey<TRole>(
            document => document.RoleId,
            foreignKey => foreignKey.DeleteAction = global::Weasel.Core.CascadeAction.Cascade,
            index => index.Name = UserRoleRoleIdIndex);

        var roleClaim = options.Schema.For<MartenIdentityRoleClaim<TRole>>();
        roleClaim.DocumentAlias(RoleClaimDocumentAlias);
        roleClaim.Duplicate(
            document => document.RoleId,
            null,
            null,
            index => index.Name = RoleClaimRoleIdIndex,
            true,
            false);
    }

    private static void ConfigureUserClaim<TUser>(StoreOptions options)
        where TUser : MartenIdentityUser
    {
        var claim = options.Schema.For<MartenIdentityUserClaim<TUser>>();
        claim.DocumentAlias(UserClaimDocumentAlias);
        claim.Duplicate(
            document => document.UserId,
            null,
            null,
            index => index.Name = UserClaimUserIdIndex,
            true,
            false);
        claim.Index(
            new Expression<Func<MartenIdentityUserClaim<TUser>, object>>[]
            {
                document => document.ClaimType!,
                document => document.ClaimValue!,
            },
            index => index.Name = UserClaimValueIndex);
    }

    private static void ConfigureUserRelationship<TDocument>(
        StoreOptions options,
        string alias,
        string indexName,
        Expression<Func<TDocument, object?>> userId)
        where TDocument : notnull
    {
        var mapping = options.Schema.For<TDocument>();
        mapping.DocumentAlias(alias);
        mapping.Duplicate(userId, null, null, index => index.Name = indexName, true, false);
    }
}
