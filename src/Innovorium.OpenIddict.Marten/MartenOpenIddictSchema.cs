using Marten;
using Marten.Schema;
using Weasel.Core;

namespace Innovorium.OpenIddict.Marten;

internal static class MartenOpenIddictSchema
{
    internal const string ApplicationAlias = "openiddict_application";
    internal const string ApplicationClientIdIndex = "uidx_openiddict_application_client_id";
    internal const string ApplicationPostLogoutRedirectUrisIndex = "idx_openiddict_application_post_logout_redirect_uris";
    internal const string ApplicationRedirectUrisIndex = "idx_openiddict_application_redirect_uris";
    internal const string AuthorizationAlias = "openiddict_authorization";
    internal const string AuthorizationApplicationIdIndex = "idx_openiddict_authorization_application_id";
    internal const string AuthorizationCreationDateIndex = "idx_openiddict_authorization_creation_date";
    internal const string AuthorizationScopesIndex = "idx_openiddict_authorization_scopes";
    internal const string AuthorizationStatusIndex = "idx_openiddict_authorization_status";
    internal const string AuthorizationSubjectIndex = "idx_openiddict_authorization_subject";
    internal const string AuthorizationTypeIndex = "idx_openiddict_authorization_type";
    internal const string ScopeAlias = "openiddict_scope";
    internal const string ScopeNameIndex = "uidx_openiddict_scope_name";
    internal const string ScopeResourcesIndex = "idx_openiddict_scope_resources";
    internal const string TokenAlias = "openiddict_token";
    internal const string TokenApplicationIdIndex = "idx_openiddict_token_application_id";
    internal const string TokenAuthorizationIdIndex = "idx_openiddict_token_authorization_id";
    internal const string TokenCreationDateIndex = "idx_openiddict_token_creation_date";
    internal const string TokenExpirationDateIndex = "idx_openiddict_token_expiration_date";
    internal const string TokenReferenceIdIndex = "uidx_openiddict_token_reference_id";
    internal const string TokenStatusIndex = "idx_openiddict_token_status";
    internal const string TokenSubjectIndex = "idx_openiddict_token_subject";
    internal const string TokenTypeIndex = "idx_openiddict_token_type";

    internal static void Configure(StoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Schema.For<OpenIddictMartenApplication>()
            .DocumentAlias(ApplicationAlias)
            .SingleTenanted()
            .UniqueIndex(UniqueIndexType.Computed, ApplicationClientIdIndex, application => application.ClientId!)
            .GinIndexJsonDataMember(
                application => application.RedirectUris,
                index => index.Name = ApplicationRedirectUrisIndex)
            .GinIndexJsonDataMember(
                application => application.PostLogoutRedirectUris,
                index => index.Name = ApplicationPostLogoutRedirectUrisIndex);

        options.Schema.For<OpenIddictMartenAuthorization>()
            .DocumentAlias(AuthorizationAlias)
            .SingleTenanted()
            .ForeignKey<OpenIddictMartenApplication>(
                authorization => authorization.ApplicationId!,
                foreignKey => foreignKey.DeleteAction = CascadeAction.Cascade,
                index => index.Name = AuthorizationApplicationIdIndex)
            .Duplicate(
                authorization => authorization.CreationDate,
                configure: index => index.Name = AuthorizationCreationDateIndex)
            .Duplicate(
                authorization => authorization.Status,
                configure: index => index.Name = AuthorizationStatusIndex)
            .Duplicate(
                authorization => authorization.Subject,
                configure: index => index.Name = AuthorizationSubjectIndex)
            .Duplicate(
                authorization => authorization.Type,
                configure: index => index.Name = AuthorizationTypeIndex)
            .GinIndexJsonDataMember(
                authorization => authorization.Scopes,
                index => index.Name = AuthorizationScopesIndex);

        options.Schema.For<OpenIddictMartenScope>()
            .DocumentAlias(ScopeAlias)
            .SingleTenanted()
            .UniqueIndex(UniqueIndexType.Computed, ScopeNameIndex, scope => scope.Name!)
            .GinIndexJsonDataMember(
                scope => scope.Resources,
                index => index.Name = ScopeResourcesIndex);

        options.Schema.For<OpenIddictMartenToken>()
            .DocumentAlias(TokenAlias)
            .SingleTenanted()
            .ForeignKey<OpenIddictMartenApplication>(
                token => token.ApplicationId!,
                foreignKey => foreignKey.DeleteAction = CascadeAction.Cascade,
                index => index.Name = TokenApplicationIdIndex)
            .ForeignKey<OpenIddictMartenAuthorization>(
                token => token.AuthorizationId!,
                foreignKey => foreignKey.DeleteAction = CascadeAction.Cascade,
                index => index.Name = TokenAuthorizationIdIndex)
            .Duplicate(
                token => token.CreationDate,
                configure: index => index.Name = TokenCreationDateIndex)
            .Duplicate(
                token => token.ExpirationDate,
                configure: index => index.Name = TokenExpirationDateIndex)
            .Duplicate(
                token => token.Status,
                configure: index => index.Name = TokenStatusIndex)
            .Duplicate(
                token => token.Subject,
                configure: index => index.Name = TokenSubjectIndex)
            .Duplicate(
                token => token.Type,
                configure: index => index.Name = TokenTypeIndex)
            .UniqueIndex(
                UniqueIndexType.Computed,
                TokenReferenceIdIndex,
                token => token.ReferenceId!);
    }
}
