using Marten;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Innovorium.AspNetCore.Identity.Marten;

/// <summary>
/// Adds Marten-backed stores to ASP.NET Core Identity.
/// </summary>
public static class IdentityBuilderExtensions
{
    internal const string UserDocumentAlias = MartenIdentitySchema.UserDocumentAlias;
    internal const string NormalizedUserNameIndex = MartenIdentitySchema.NormalizedUserNameIndex;
    internal const string NormalizedEmailIndex = MartenIdentitySchema.NormalizedEmailIndex;
    internal const string UniqueNormalizedEmailIndex = MartenIdentitySchema.UniqueNormalizedEmailIndex;

    /// <summary>
    /// Adds complete Marten-backed user stores and, when configured, role stores to an Identity builder.
    /// </summary>
    /// <remarks>
    /// The application remains responsible for configuring Marten's connection and schema lifecycle.
    /// Both user-only <c>AddIdentityCore&lt;TUser&gt;()</c> and role-enabled builders are supported.
    /// </remarks>
    /// <param name="builder">The Identity builder to extend.</param>
    /// <returns>The same builder so that additional Identity services can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">A configured user or role type does not derive from the corresponding Marten document type.</exception>
    public static IdentityBuilder AddMartenStores(this IdentityBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (!typeof(MartenIdentityUser).IsAssignableFrom(builder.UserType))
        {
            throw new InvalidOperationException(
                $"The configured user type '{builder.UserType}' must derive from {nameof(MartenIdentityUser)}.");
        }

        if (builder.RoleType is not null && !typeof(MartenIdentityRole).IsAssignableFrom(builder.RoleType))
        {
            throw new InvalidOperationException(
                $"The configured role type '{builder.RoleType}' must derive from {nameof(MartenIdentityRole)}.");
        }

        var methodName = builder.RoleType is null
            ? nameof(AddMartenStoresForUser)
            : nameof(AddMartenStoresForUserAndRole);
        var typeArguments = builder.RoleType is null
            ? new[] { builder.UserType }
            : new[] { builder.UserType, builder.RoleType };
        var method = typeof(IdentityBuilderExtensions)
            .GetMethod(methodName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .MakeGenericMethod(typeArguments);

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
            MartenIdentitySchema.ConfigureUser<TUser>(
                options,
                serviceProvider.GetRequiredService<IOptions<IdentityOptions>>().Value.User.RequireUniqueEmail));

        builder.Services.RemoveAll<IUserStore<TUser>>();
        builder.Services.AddScoped<IUserStore<TUser>, MartenUserOnlyStore<TUser>>();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IUserValidator<TUser>, MartenPendingUserChangesValidator<TUser>>());
        return builder;
    }

    private static IdentityBuilder AddMartenStoresForUserAndRole<TUser, TRole>(IdentityBuilder builder)
        where TUser : MartenIdentityUser
        where TRole : MartenIdentityRole
    {
        if (!builder.Services.Any(descriptor => descriptor.ServiceType == typeof(MartenStoreRegistration<TUser>)))
        {
            builder.Services.AddSingleton<MartenStoreRegistration<TUser>>();
            builder.Services.ConfigureMarten((serviceProvider, options) =>
                MartenIdentitySchema.ConfigureUser<TUser>(
                    options,
                    serviceProvider.GetRequiredService<IOptions<IdentityOptions>>().Value.User.RequireUniqueEmail));
        }

        if (!builder.Services.Any(descriptor => descriptor.ServiceType == typeof(MartenRoleStoreRegistration<TUser, TRole>)))
        {
            builder.Services.AddSingleton<MartenRoleStoreRegistration<TUser, TRole>>();
            builder.Services.ConfigureMarten(options => MartenIdentitySchema.ConfigureRoles<TUser, TRole>(options));
        }

        builder.Services.RemoveAll<IUserStore<TUser>>();
        builder.Services.AddScoped<IUserStore<TUser>, MartenUserStore<TUser, TRole>>();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IUserValidator<TUser>, MartenPendingUserChangesValidator<TUser>>());
        builder.Services.RemoveAll<IRoleStore<TRole>>();
        builder.Services.AddScoped<IRoleStore<TRole>, MartenRoleStore<TRole>>();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IRoleValidator<TRole>, MartenPendingRoleChangesValidator<TRole>>());
        return builder;
    }

    private sealed class MartenStoreRegistration<TUser>
        where TUser : MartenIdentityUser;

    private sealed class MartenRoleStoreRegistration<TUser, TRole>
        where TUser : MartenIdentityUser
        where TRole : MartenIdentityRole;
}
