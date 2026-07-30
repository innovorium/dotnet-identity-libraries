using JasperFx.MultiTenancy;
using Marten;
using Marten.Schema;
using Marten.Storage;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Xunit;

namespace Innovorium.OpenIddict.Marten.Tests;

public sealed class RegistrationTests
{
    [Fact]
    public void UseMartenRegistersOnlyApplicationAndScopeStoresAsScoped()
    {
        var services = new ServiceCollection();
        var builder = new OpenIddictCoreBuilder(services);

        var result = builder.UseMarten();

        Assert.Same(builder, result);
        AssertStore<MartenOpenIddictApplicationStore,
            IOpenIddictApplicationStore<OpenIddictMartenApplication>>(services);
        AssertStore<MartenOpenIddictScopeStore,
            IOpenIddictScopeStore<OpenIddictMartenScope>>(services);
        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType.IsGenericType &&
            descriptor.ServiceType.GetGenericTypeDefinition() == typeof(IOpenIddictAuthorizationStore<>));
        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType.IsGenericType &&
            descriptor.ServiceType.GetGenericTypeDefinition() == typeof(IOpenIddictTokenStore<>));
    }

    [Fact]
    public void UseMartenIsIdempotentForStoreRegistrations()
    {
        var services = new ServiceCollection();
        var builder = new OpenIddictCoreBuilder(services);

        builder.UseMarten();
        builder.UseMarten();

        Assert.Single(services, descriptor =>
            descriptor.ServiceType == typeof(IOpenIddictApplicationStore<OpenIddictMartenApplication>));
        Assert.Single(services, descriptor =>
            descriptor.ServiceType == typeof(IOpenIddictScopeStore<OpenIddictMartenScope>));
    }

    [Fact]
    public void RegisteredStoresResolveFromTheCustomerScope()
    {
        var services = new ServiceCollection();
        var session = RecordingDocumentSession.Create(out _);
        services.AddSingleton(session);
        new OpenIddictCoreBuilder(services).UseMarten();

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();

        Assert.IsType<MartenOpenIddictApplicationStore>(scope.ServiceProvider.GetRequiredService<
            IOpenIddictApplicationStore<OpenIddictMartenApplication>>());
        Assert.IsType<MartenOpenIddictScopeStore>(scope.ServiceProvider.GetRequiredService<
            IOpenIddictScopeStore<OpenIddictMartenScope>>());
    }

    [Fact]
    public void SchemaHasStableAliasesConcurrencyAndNamedIndexes()
    {
        using var store = DocumentStore.For(options =>
        {
            options.Connection("Host=localhost;Database=unused;Username=unused");
            MartenOpenIddictSchema.Configure(options);
        });

        var ddl = store.Storage.ToDatabaseScript();

        Assert.Contains($"mt_doc_{MartenOpenIddictSchema.ApplicationAlias}", ddl, StringComparison.Ordinal);
        Assert.Contains(MartenOpenIddictSchema.ApplicationClientIdIndex, ddl, StringComparison.Ordinal);
        Assert.Contains(MartenOpenIddictSchema.ApplicationRedirectUrisIndex, ddl, StringComparison.Ordinal);
        Assert.Contains(MartenOpenIddictSchema.ApplicationPostLogoutRedirectUrisIndex, ddl, StringComparison.Ordinal);
        Assert.Contains($"mt_doc_{MartenOpenIddictSchema.ScopeAlias}", ddl, StringComparison.Ordinal);
        Assert.Contains(MartenOpenIddictSchema.ScopeNameIndex, ddl, StringComparison.Ordinal);
        Assert.Contains(MartenOpenIddictSchema.ScopeResourcesIndex, ddl, StringComparison.Ordinal);
        Assert.Contains("mt_version", ddl, StringComparison.Ordinal);
        Assert.Contains("integer", ddl, StringComparison.OrdinalIgnoreCase);

        IReadOnlyStoreOptions readOnlyOptions = store.Options;
        Assert.Equal(
            TenancyStyle.Single,
            readOnlyOptions.FindOrResolveDocumentType(typeof(OpenIddictMartenApplication)).TenancyStyle);
        Assert.Equal(
            TenancyStyle.Single,
            readOnlyOptions.FindOrResolveDocumentType(typeof(OpenIddictMartenScope)).TenancyStyle);
    }

    [Fact]
    public void RepeatedUseMartenDoesNotDuplicateMappingsOrIndexes()
    {
        var services = new ServiceCollection();
        services.AddMarten(options =>
            options.Connection("Host=localhost;Database=unused;Username=unused"));
        var builder = new OpenIddictCoreBuilder(services);
        builder.UseMarten();
        builder.UseMarten();

        using var provider = services.BuildServiceProvider();
        var ddl = provider.GetRequiredService<IDocumentStore>().Storage.ToDatabaseScript();

        Assert.Equal(1, CountOccurrences(ddl, MartenOpenIddictSchema.ApplicationClientIdIndex));
        Assert.Equal(1, CountOccurrences(ddl, MartenOpenIddictSchema.ApplicationRedirectUrisIndex));
        Assert.Equal(1, CountOccurrences(ddl, MartenOpenIddictSchema.ScopeNameIndex));
        Assert.Equal(1, CountOccurrences(ddl, MartenOpenIddictSchema.ScopeResourcesIndex));
    }

    private static void AssertStore<TImplementation, TService>(IServiceCollection services)
    {
        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(TService));
        Assert.Equal(typeof(TImplementation), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    private static int CountOccurrences(string value, string search)
        => value.Split(search, StringSplitOptions.None).Length - 1;
}
