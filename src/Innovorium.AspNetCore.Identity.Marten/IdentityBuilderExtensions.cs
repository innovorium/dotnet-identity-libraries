using Marten;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Innovorium.AspNetCore.Identity.Marten;

/// <summary>
/// Adds Marten-backed stores to ASP.NET Core Identity.
/// </summary>
public static class IdentityBuilderExtensions
{
    internal const string UserDocumentAlias = "identity_user";
    internal const string NormalizedUserNameIndex = "uidx_identity_user_normalized_username";
    internal const string NormalizedEmailIndex = "idx_identity_user_normalized_email";
    internal const string UniqueNormalizedEmailIndex = "uidx_identity_user_normalized_email";

    /// <summary>
    /// Adds the Marten user store and its document mapping to an Identity builder.
    /// </summary>
    /// <remarks>
    /// The application remains responsible for configuring Marten's connection and schema lifecycle.
    /// Role stores are not included in this initial provider slice.
    /// </remarks>
    /// <param name="builder">The Identity builder to extend.</param>
    /// <returns>The same builder so that additional Identity services can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The Identity builder has roles enabled.</exception>
    public static IdentityBuilder AddMartenStores(this IdentityBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (builder.RoleType is not null)
        {
            throw new NotSupportedException(
                "Innovorium.AspNetCore.Identity.Marten currently supports user-only IdentityBuilder registrations. " +
                "Use AddIdentityCore<TUser>() until Marten role stores are available.");
        }

        if (!typeof(MartenIdentityUser).IsAssignableFrom(builder.UserType))
        {
            throw new InvalidOperationException(
                $"The configured user type '{builder.UserType}' must derive from {nameof(MartenIdentityUser)}.");
        }

        var method = typeof(IdentityBuilderExtensions)
            .GetMethod(nameof(AddMartenStoresForUser), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .MakeGenericMethod(builder.UserType);

        return (IdentityBuilder)method.Invoke(null, [builder])!;
    }

    private static IdentityBuilder AddMartenStoresForUser<TUser>(IdentityBuilder builder)
        where TUser : MartenIdentityUser
    {
        if (builder.Services.Any(descriptor => descriptor.ServiceType == typeof(MartenStoreRegistration<TUser>)))
        {
            return builder;
        }

        builder.Services.AddSingleton<MartenStoreRegistration<TUser>>();
        builder.Services.ConfigureMarten((serviceProvider, options) =>
        {
            var identityOptions = serviceProvider.GetRequiredService<IOptions<IdentityOptions>>().Value;
            var mapping = options.Schema.For<TUser>();

            mapping.DocumentAlias(UserDocumentAlias);
            mapping.UseOptimisticConcurrency(true);
            mapping.UniqueIndex(NormalizedUserNameIndex, user => user.NormalizedUserName!);

            if (identityOptions.User.RequireUniqueEmail)
            {
                mapping.UniqueIndex(UniqueNormalizedEmailIndex, user => user.NormalizedEmail!);
            }
            else
            {
                mapping.Index(user => user.NormalizedEmail!, index => index.Name = NormalizedEmailIndex);
            }
        });

        builder.Services.AddScoped<IUserStore<TUser>, MartenUserOnlyStore<TUser>>();
        return builder;
    }

    private sealed class MartenStoreRegistration<TUser>
        where TUser : MartenIdentityUser;
}
