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
    public void UseMartenRegistersAndResolvesAllFourScopedStores()
    {
        var services = new ServiceCollection();
        var documentStore = RecordingDocumentSession.Create(out _);
        services.AddSingleton(documentStore);
        var builder = new OpenIddictCoreBuilder(services);

        var result = builder.UseMarten();

        Assert.Same(builder, result);
        AssertStore<MartenOpenIddictApplicationStore,
            IOpenIddictApplicationStore<OpenIddictMartenApplication>>(services);
        AssertStore<MartenOpenIddictAuthorizationStore,
            IOpenIddictAuthorizationStore<OpenIddictMartenAuthorization>>(services);
        AssertStore<MartenOpenIddictScopeStore,
            IOpenIddictScopeStore<OpenIddictMartenScope>>(services);
        AssertStore<MartenOpenIddictTokenStore,
            IOpenIddictTokenStore<OpenIddictMartenToken>>(services);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(TimeProvider) &&
            descriptor.Lifetime == ServiceLifetime.Singleton);

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();

        Assert.IsType<MartenOpenIddictApplicationStore>(scope.ServiceProvider.GetRequiredService<
            IOpenIddictApplicationStore<OpenIddictMartenApplication>>());
        Assert.IsType<MartenOpenIddictAuthorizationStore>(scope.ServiceProvider.GetRequiredService<
            IOpenIddictAuthorizationStore<OpenIddictMartenAuthorization>>());
        Assert.IsType<MartenOpenIddictScopeStore>(scope.ServiceProvider.GetRequiredService<
            IOpenIddictScopeStore<OpenIddictMartenScope>>());
        Assert.IsType<MartenOpenIddictTokenStore>(scope.ServiceProvider.GetRequiredService<
            IOpenIddictTokenStore<OpenIddictMartenToken>>());
    }

    [Fact]
    public void RepeatedUseMartenDoesNotDuplicateServicesMappingsOrIndexes()
    {
        var services = new ServiceCollection();
        services.AddMarten(options =>
            options.Connection("Host=localhost;Database=unused;Username=unused"));
        var builder = new OpenIddictCoreBuilder(services);

        builder.UseMarten();
        builder.UseMarten();

        Assert.Single(services, descriptor =>
            descriptor.ServiceType == typeof(IOpenIddictApplicationStore<OpenIddictMartenApplication>));
        Assert.Single(services, descriptor =>
            descriptor.ServiceType == typeof(IOpenIddictAuthorizationStore<OpenIddictMartenAuthorization>));
        Assert.Single(services, descriptor =>
            descriptor.ServiceType == typeof(IOpenIddictScopeStore<OpenIddictMartenScope>));
        Assert.Single(services, descriptor =>
            descriptor.ServiceType == typeof(IOpenIddictTokenStore<OpenIddictMartenToken>));

        using var provider = services.BuildServiceProvider();
        var ddl = provider.GetRequiredService<IDocumentStore>().Storage.ToDatabaseScript();

        Assert.Equal(1, CountOccurrences(ddl, MartenOpenIddictSchema.ApplicationClientIdIndex));
        Assert.Equal(1, CountOccurrences(ddl, MartenOpenIddictSchema.ApplicationRedirectUrisIndex));
        Assert.Equal(1, CountOccurrences(ddl, MartenOpenIddictSchema.AuthorizationApplicationIdIndex));
        Assert.Equal(1, CountOccurrences(ddl, MartenOpenIddictSchema.AuthorizationScopesIndex));
        Assert.Equal(1, CountOccurrences(ddl, MartenOpenIddictSchema.ScopeNameIndex));
        Assert.Equal(1, CountOccurrences(ddl, MartenOpenIddictSchema.ScopeResourcesIndex));
        Assert.Equal(1, CountOccurrences(ddl, MartenOpenIddictSchema.TokenApplicationIdIndex));
        Assert.Equal(1, CountOccurrences(ddl, MartenOpenIddictSchema.TokenAuthorizationIdIndex));
        Assert.Equal(1, CountOccurrences(ddl, MartenOpenIddictSchema.TokenReferenceIdIndex));
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
        Assert.Contains($"mt_doc_{MartenOpenIddictSchema.AuthorizationAlias}", ddl, StringComparison.Ordinal);
        Assert.Contains(MartenOpenIddictSchema.AuthorizationApplicationIdIndex, ddl, StringComparison.Ordinal);
        Assert.Contains(MartenOpenIddictSchema.AuthorizationCreationDateIndex, ddl, StringComparison.Ordinal);
        Assert.Contains(MartenOpenIddictSchema.AuthorizationScopesIndex, ddl, StringComparison.Ordinal);
        Assert.Contains(MartenOpenIddictSchema.AuthorizationStatusIndex, ddl, StringComparison.Ordinal);
        Assert.Contains($"mt_doc_{MartenOpenIddictSchema.ScopeAlias}", ddl, StringComparison.Ordinal);
        Assert.Contains(MartenOpenIddictSchema.ScopeNameIndex, ddl, StringComparison.Ordinal);
        Assert.Contains(MartenOpenIddictSchema.ScopeResourcesIndex, ddl, StringComparison.Ordinal);
        Assert.Contains($"mt_doc_{MartenOpenIddictSchema.TokenAlias}", ddl, StringComparison.Ordinal);
        Assert.Contains(MartenOpenIddictSchema.TokenApplicationIdIndex, ddl, StringComparison.Ordinal);
        Assert.Contains(MartenOpenIddictSchema.TokenAuthorizationIdIndex, ddl, StringComparison.Ordinal);
        Assert.Contains(MartenOpenIddictSchema.TokenReferenceIdIndex, ddl, StringComparison.Ordinal);
        Assert.Contains("on delete cascade", ddl, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("mt_version", ddl, StringComparison.Ordinal);
        Assert.Contains("integer", ddl, StringComparison.OrdinalIgnoreCase);

        IReadOnlyStoreOptions readOnlyOptions = store.Options;
        Assert.Equal(
            TenancyStyle.Single,
            readOnlyOptions.FindOrResolveDocumentType(typeof(OpenIddictMartenApplication)).TenancyStyle);
        Assert.Equal(
            TenancyStyle.Single,
            readOnlyOptions.FindOrResolveDocumentType(typeof(OpenIddictMartenAuthorization)).TenancyStyle);
        Assert.Equal(
            TenancyStyle.Single,
            readOnlyOptions.FindOrResolveDocumentType(typeof(OpenIddictMartenScope)).TenancyStyle);
        Assert.Equal(
            TenancyStyle.Single,
            readOnlyOptions.FindOrResolveDocumentType(typeof(OpenIddictMartenToken)).TenancyStyle);
    }

    [Fact]
    public void UseMartenPreservesACustomerTimeProvider()
    {
        var services = new ServiceCollection();
        var expected = new TestTimeProvider();
        services.AddSingleton<TimeProvider>(expected);

        new OpenIddictCoreBuilder(services).UseMarten();

        using var provider = services.BuildServiceProvider();
        Assert.Same(expected, provider.GetRequiredService<TimeProvider>());
    }

    private static void AssertStore<TImplementation, TService>(IServiceCollection services)
    {
        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(TService));
        Assert.Equal(typeof(TImplementation), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    private static int CountOccurrences(string value, string search)
        => value.Split(search, StringSplitOptions.None).Length - 1;

    private sealed class TestTimeProvider : TimeProvider;
}
