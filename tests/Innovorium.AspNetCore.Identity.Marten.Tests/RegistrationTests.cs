using JasperFx;
using Marten;
using Marten.Schema;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Innovorium.AspNetCore.Identity.Marten.Tests;

public sealed class RegistrationTests
{
    private const string TestConnectionString =
        "Host=127.0.0.1;Port=1;Database=identity_tests;Username=identity_tests;Password=identity_tests;Timeout=1";

    [Fact]
    public void AddMartenStoresRegistersTheUserStoreAndManagerCapabilities()
    {
        var services = CreateServices();
        services.AddIdentityCore<ApplicationUser>().AddMartenStores();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IUserStore<ApplicationUser>>();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        Assert.IsType<MartenUserOnlyStore<ApplicationUser>>(store);
        Assert.True(manager.SupportsQueryableUsers);
        Assert.True(manager.SupportsUserPassword);
        Assert.True(manager.SupportsUserEmail);
        Assert.True(manager.SupportsUserPhoneNumber);
        Assert.True(manager.SupportsUserSecurityStamp);
        Assert.True(manager.SupportsUserLockout);
        Assert.True(manager.SupportsUserTwoFactor);
        Assert.True(manager.SupportsUserClaim);
        Assert.True(manager.SupportsUserLogin);
        Assert.True(manager.SupportsUserAuthenticationTokens);
        Assert.True(manager.SupportsUserAuthenticatorKey);
        Assert.True(manager.SupportsUserTwoFactorRecoveryCodes);
        Assert.True(manager.SupportsUserPasskey);
        Assert.False(manager.SupportsUserRole);
    }

    [Fact]
    public void AddMartenStoresRegistersStableMappingWithoutChangingSchemaOwnership()
    {
        var services = new ServiceCollection();
        AutoCreate? observedAutoCreate = null;
        services.AddMarten(options =>
        {
            options.Connection(TestConnectionString);
            options.AutoCreateSchemaObjects = AutoCreate.None;
        });
        services.AddIdentityCore<ApplicationUser>().AddMartenStores();
        services.ConfigureMarten(options => observedAutoCreate = options.AutoCreateSchemaObjects);

        using var provider = services.BuildServiceProvider();
        using var documentStore = provider.GetRequiredService<IDocumentStore>();
        var mapping = Assert.IsAssignableFrom<DocumentMapping>(
            documentStore.Options.FindOrResolveDocumentType(typeof(ApplicationUser)));

        Assert.Equal(AutoCreate.None, observedAutoCreate);
        Assert.Equal("identity_user", mapping.Alias);
        Assert.True(mapping.UseOptimisticConcurrency);
        Assert.Contains(mapping.Indexes, index => index.Name == "uidx_identity_user_normalized_username");
        Assert.Contains(mapping.Indexes, index => index.Name == "idx_identity_user_normalized_email");
    }

    [Fact]
    public void RequireUniqueEmailUsesTheUniqueEmailIndex()
    {
        var services = CreateServices();
        services.Configure<IdentityOptions>(options => options.User.RequireUniqueEmail = true);
        services.AddIdentityCore<ApplicationUser>().AddMartenStores();

        using var provider = services.BuildServiceProvider();
        using var documentStore = provider.GetRequiredService<IDocumentStore>();
        var mapping = Assert.IsAssignableFrom<DocumentMapping>(
            documentStore.Options.FindOrResolveDocumentType(typeof(ApplicationUser)));

        Assert.Contains(mapping.Indexes, index => index.Name == "uidx_identity_user_normalized_email");
        Assert.DoesNotContain(mapping.Indexes, index => index.Name == "idx_identity_user_normalized_email");
    }

    [Fact]
    public void RepeatedRegistrationIsIdempotentForTheSameUserType()
    {
        var services = CreateServices();
        var builder = services.AddIdentityCore<ApplicationUser>();
        var configurationsBeforeRegistration = services.Count(descriptor =>
            descriptor.ServiceType == typeof(IConfigureMarten));

        builder.AddMartenStores();
        var configurationsAfterFirstRegistration = services.Count(descriptor =>
            descriptor.ServiceType == typeof(IConfigureMarten));
        builder.AddMartenStores();
        var configurationsAfterSecondRegistration = services.Count(descriptor =>
            descriptor.ServiceType == typeof(IConfigureMarten));

        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IUserStore<ApplicationUser>));
        Assert.Equal(configurationsBeforeRegistration + 1, configurationsAfterFirstRegistration);
        Assert.Equal(configurationsAfterFirstRegistration, configurationsAfterSecondRegistration);

        using var provider = services.BuildServiceProvider();
        using var documentStore = provider.GetRequiredService<IDocumentStore>();
        var mapping = Assert.IsAssignableFrom<DocumentMapping>(
            documentStore.Options.FindOrResolveDocumentType(typeof(ApplicationUser)));

        Assert.Single(mapping.Indexes, index => index.Name == "uidx_identity_user_normalized_username");
        Assert.Single(mapping.Indexes, index => index.Name == "idx_identity_user_normalized_email");
    }

    [Fact]
    public void RoleEnabledIdentityRegistersUserRoleAndManagerCapabilities()
    {
        var services = CreateServices();
        var builder = services.AddIdentityCore<ApplicationUser>().AddRoles<ApplicationRole>();
        builder.AddMartenStores();
        builder.AddMartenStores();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var userStore = scope.ServiceProvider.GetRequiredService<IUserStore<ApplicationUser>>();
        var roleStore = scope.ServiceProvider.GetRequiredService<IRoleStore<ApplicationRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

        Assert.IsType<MartenUserStore<ApplicationUser, ApplicationRole>>(userStore);
        Assert.IsType<MartenRoleStore<ApplicationRole>>(roleStore);
        Assert.True(userManager.SupportsUserRole);
        Assert.True(roleManager.SupportsQueryableRoles);
        Assert.True(roleManager.SupportsRoleClaims);
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IUserStore<ApplicationUser>));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IRoleStore<ApplicationRole>));

        using var documentStore = provider.GetRequiredService<IDocumentStore>();
        var roleMapping = documentStore.Options.FindOrResolveDocumentType(typeof(ApplicationRole));
        var membershipMapping = documentStore.Options.FindOrResolveDocumentType(typeof(MartenIdentityUserRole));
        var roleClaimMapping = documentStore.Options.FindOrResolveDocumentType(
            typeof(MartenIdentityRoleClaim<ApplicationRole>));
        Assert.Equal(MartenIdentitySchema.RoleDocumentAlias, roleMapping.Alias);
        Assert.True(roleMapping.UseOptimisticConcurrency);
        Assert.Contains(roleMapping.Indexes, index => index.Name == MartenIdentitySchema.NormalizedRoleNameIndex);
        Assert.Equal(MartenIdentitySchema.UserRoleDocumentAlias, membershipMapping.Alias);
        Assert.Contains(
            membershipMapping.Indexes,
            index => index.Name == MartenIdentitySchema.UserRoleUserIdIndex);
        Assert.Contains(
            membershipMapping.Indexes,
            index => index.Name == MartenIdentitySchema.UserRoleRoleIdIndex);
        Assert.Equal(MartenIdentitySchema.RoleClaimDocumentAlias, roleClaimMapping.Alias);
    }

    [Fact]
    public void RoleMustDeriveFromMartenIdentityRole()
    {
        var services = CreateServices();
        var builder = services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>();

        var exception = Assert.Throws<InvalidOperationException>(() => builder.AddMartenStores());

        Assert.Contains(nameof(MartenIdentityRole), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UserMustDeriveFromMartenIdentityUser()
    {
        var services = CreateServices();
        var builder = services.AddIdentityCore<IdentityUser>();

        var exception = Assert.Throws<InvalidOperationException>(() => builder.AddMartenStores());

        Assert.Contains(nameof(MartenIdentityUser), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProtectedPersonalDataIsRejectedAtStoreActivation()
    {
        using var documentStore = DocumentStore.For(options => options.Connection(TestConnectionString));
        var options = Options.Create(new IdentityOptions());
        options.Value.Stores.ProtectPersonalData = true;

        var exception = Assert.Throws<NotSupportedException>(() =>
            new MartenUserOnlyStore<ApplicationUser>(documentStore, options, new IdentityErrorDescriber()));

        Assert.Contains(nameof(IProtectedUserStore<ApplicationUser>), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConjoinedTenancyIsRejectedAtStoreActivation()
    {
        using var documentStore = DocumentStore.For(options =>
        {
            options.Connection(TestConnectionString);
            options.Schema.For<ApplicationUser>().MultiTenanted();
        });

        var exception = Assert.Throws<NotSupportedException>(() =>
            new MartenUserOnlyStore<ApplicationUser>(
                documentStore,
                Options.Create(new IdentityOptions()),
                new IdentityErrorDescriber()));

        Assert.Contains("single-tenanted", exception.Message, StringComparison.Ordinal);
        Assert.Contains("SingleTenanted", exception.Message, StringComparison.Ordinal);
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMarten(options => options.Connection(TestConnectionString));
        return services;
    }

    private sealed class ApplicationUser : MartenIdentityUser;

    private sealed class ApplicationRole : MartenIdentityRole;
}
