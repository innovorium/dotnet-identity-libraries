using Innovorium.OpenIddict.Marten;
using Marten;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// OpenIddict core extensions for the Marten stores.
/// </summary>
public static class OpenIddictMartenExtensions
{
    /// <summary>
    /// Registers the Marten-backed OpenIddict application and scope stores.
    /// </summary>
    /// <remarks>
    /// This method does not configure a PostgreSQL connection, apply schema changes,
    /// or register authorization and token stores.
    /// </remarks>
    /// <param name="builder">The OpenIddict core builder.</param>
    /// <returns>The OpenIddict core builder.</returns>
    public static OpenIddictCoreBuilder UseMarten(this OpenIddictCoreBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.SetDefaultApplicationEntity<OpenIddictMartenApplication>()
            .ReplaceApplicationStore<OpenIddictMartenApplication, MartenOpenIddictApplicationStore>(
                ServiceLifetime.Scoped)
            .SetDefaultScopeEntity<OpenIddictMartenScope>()
            .ReplaceScopeStore<OpenIddictMartenScope, MartenOpenIddictScopeStore>(
                ServiceLifetime.Scoped);

        if (!builder.Services.Any(descriptor => descriptor.ServiceType == typeof(MartenOpenIddictRegistrationMarker)))
        {
            builder.Services.AddSingleton<MartenOpenIddictRegistrationMarker>();
            builder.Services.ConfigureMarten(MartenOpenIddictSchema.Configure);
        }

        return builder;
    }

    private sealed class MartenOpenIddictRegistrationMarker;
}
