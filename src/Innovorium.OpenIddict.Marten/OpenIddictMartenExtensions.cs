using Innovorium.OpenIddict.Marten;
using Marten;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// OpenIddict core extensions for the Marten stores.
/// </summary>
public static class OpenIddictMartenExtensions
{
    /// <summary>
    /// Registers the Marten-backed OpenIddict application, authorization, scope, and token stores.
    /// </summary>
    /// <remarks>
    /// This method does not configure a PostgreSQL connection or apply schema changes.
    /// The host can register a custom <see cref="TimeProvider"/> before calling this method;
    /// otherwise, <see cref="TimeProvider.System"/> is used for token pruning.
    /// </remarks>
    /// <param name="builder">The OpenIddict core builder.</param>
    /// <returns>The OpenIddict core builder.</returns>
    public static OpenIddictCoreBuilder UseMarten(this OpenIddictCoreBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.SetDefaultApplicationEntity<OpenIddictMartenApplication>()
            .ReplaceApplicationStore<OpenIddictMartenApplication, MartenOpenIddictApplicationStore>(
                ServiceLifetime.Scoped)
            .SetDefaultAuthorizationEntity<OpenIddictMartenAuthorization>()
            .ReplaceAuthorizationStore<OpenIddictMartenAuthorization, MartenOpenIddictAuthorizationStore>(
                ServiceLifetime.Scoped)
            .SetDefaultScopeEntity<OpenIddictMartenScope>()
            .ReplaceScopeStore<OpenIddictMartenScope, MartenOpenIddictScopeStore>(
                ServiceLifetime.Scoped)
            .SetDefaultTokenEntity<OpenIddictMartenToken>()
            .ReplaceTokenStore<OpenIddictMartenToken, MartenOpenIddictTokenStore>(
                ServiceLifetime.Scoped);

        builder.Services.TryAddSingleton(TimeProvider.System);

        if (!builder.Services.Any(descriptor => descriptor.ServiceType == typeof(MartenOpenIddictRegistrationMarker)))
        {
            builder.Services.AddSingleton<MartenOpenIddictRegistrationMarker>();
            builder.Services.ConfigureMarten(MartenOpenIddictSchema.Configure);
        }

        return builder;
    }

    private sealed class MartenOpenIddictRegistrationMarker;
}
