using Marten;
using Marten.Schema;

namespace Innovorium.OpenIddict.Marten;

internal static class MartenOpenIddictSchema
{
    internal const string ApplicationAlias = "openiddict_application";
    internal const string ApplicationClientIdIndex = "uidx_openiddict_application_client_id";
    internal const string ApplicationPostLogoutRedirectUrisIndex = "idx_openiddict_application_post_logout_redirect_uris";
    internal const string ApplicationRedirectUrisIndex = "idx_openiddict_application_redirect_uris";
    internal const string ScopeAlias = "openiddict_scope";
    internal const string ScopeNameIndex = "uidx_openiddict_scope_name";
    internal const string ScopeResourcesIndex = "idx_openiddict_scope_resources";

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

        options.Schema.For<OpenIddictMartenScope>()
            .DocumentAlias(ScopeAlias)
            .SingleTenanted()
            .UniqueIndex(UniqueIndexType.Computed, ScopeNameIndex, scope => scope.Name!)
            .GinIndexJsonDataMember(
                scope => scope.Resources,
                index => index.Name = ScopeResourcesIndex);
    }
}
