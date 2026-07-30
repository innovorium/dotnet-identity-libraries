using JasperFx;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OpenIddict.Abstractions;
using Xunit;

namespace Innovorium.OpenIddict.Marten.Tests;

public sealed class PostgreSqlIntegrationTests
{
    private const string ConnectionVariable = "INNOVORIUM_TEST_POSTGRES";

    public static bool HasPostgreSql =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable));

    [Fact(
        Skip = "Set INNOVORIUM_TEST_POSTGRES to run the disposable PostgreSQL integration test.",
        SkipUnless = nameof(HasPostgreSql))]
    public async Task CustomerRegistrationResolvesManagersAndSupportsLookupsAndUniqueness()
    {
        await using var provider = await CreateProviderAsync();
        await using var scope = provider.CreateAsyncScope();
        var applicationManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var scopeManager = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
        var applicationStore = scope.ServiceProvider.GetRequiredService<
            IOpenIddictApplicationStore<OpenIddictMartenApplication>>();
        var scopeStore = scope.ServiceProvider.GetRequiredService<
            IOpenIddictScopeStore<OpenIddictMartenScope>>();
        var suffix = Guid.NewGuid().ToString("N");
        var clientId = $"customer-portal-{suffix}";
        var scopeName = $"profile-{suffix}";
        var redirectUri = $"https://customer.example.test/callback/{suffix}";
        var resource = $"customer-api-{suffix}";

        await applicationManager.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientType = OpenIddictConstants.ClientTypes.Public,
            DisplayName = "Customer portal",
            RedirectUris = { new Uri(redirectUri) },
        }, TestContext.Current.CancellationToken);
        await scopeManager.CreateAsync(new OpenIddictScopeDescriptor
        {
            Name = scopeName,
            DisplayName = "Customer profile",
            Resources = { resource },
        }, TestContext.Current.CancellationToken);

        Assert.NotNull(await applicationManager.FindByClientIdAsync(
            clientId,
            TestContext.Current.CancellationToken));
        Assert.NotNull(await scopeManager.FindByNameAsync(
            scopeName,
            TestContext.Current.CancellationToken));
        Assert.Single(await applicationStore.FindByRedirectUriAsync(
            redirectUri,
            TestContext.Current.CancellationToken).ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await scopeStore.FindByNamesAsync(
            [scopeName],
            TestContext.Current.CancellationToken).ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await scopeStore.FindByResourceAsync(
            resource,
            TestContext.Current.CancellationToken).ToListAsync(TestContext.Current.CancellationToken));

        var duplicate = new OpenIddictMartenApplication { ClientId = clientId };
        var exception = await Assert.ThrowsAnyAsync<Exception>(async () =>
            await applicationStore.CreateAsync(duplicate, TestContext.Current.CancellationToken));

        Assert.True(ContainsPostgreSqlUniqueViolation(exception));
        Assert.Equal(1, await applicationStore.CountAsync(
            query => query.Where(application => application.ClientId == clientId),
            TestContext.Current.CancellationToken));
    }

    [Fact(
        Skip = "Set INNOVORIUM_TEST_POSTGRES to run the disposable PostgreSQL integration test.",
        SkipUnless = nameof(HasPostgreSql))]
    public async Task IndependentSessionsRejectStaleAndMissingMutationsAndPreserveSurvivors()
    {
        await using var provider = await CreateProviderAsync();

        await VerifyApplicationConcurrencyAsync(provider);
        await VerifyScopeConcurrencyAsync(provider);
    }

    private static async Task VerifyApplicationConcurrencyAsync(ServiceProvider provider)
    {
        var application = new OpenIddictMartenApplication
        {
            ClientId = $"concurrency-client-{Guid.NewGuid():N}",
            DisplayName = "Before",
        };
        await using (var createScope = provider.CreateAsyncScope())
        {
            await createScope.ServiceProvider.GetRequiredService<
                IOpenIddictApplicationStore<OpenIddictMartenApplication>>()
                .CreateAsync(application, TestContext.Current.CancellationToken);
        }

        await using var staleScope = provider.CreateAsyncScope();
        await using var currentScope = provider.CreateAsyncScope();
        var staleStore = staleScope.ServiceProvider.GetRequiredService<
            IOpenIddictApplicationStore<OpenIddictMartenApplication>>();
        var currentStore = currentScope.ServiceProvider.GetRequiredService<
            IOpenIddictApplicationStore<OpenIddictMartenApplication>>();
        var stale = Assert.IsType<OpenIddictMartenApplication>(await staleStore.FindByIdAsync(
            application.Id.ToString("D"),
            TestContext.Current.CancellationToken));
        var current = Assert.IsType<OpenIddictMartenApplication>(await currentStore.FindByIdAsync(
            application.Id.ToString("D"),
            TestContext.Current.CancellationToken));

        current.DisplayName = "Current";
        await currentStore.UpdateAsync(current, TestContext.Current.CancellationToken);

        stale.DisplayName = "Stale";
        await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(async () =>
            await staleStore.UpdateAsync(stale, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(async () =>
            await staleStore.DeleteAsync(stale, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(async () =>
            await currentStore.UpdateAsync(
                new OpenIddictMartenApplication { Id = Guid.NewGuid(), Version = 1 },
                TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(async () =>
            await currentStore.DeleteAsync(
                new OpenIddictMartenApplication { Id = Guid.NewGuid(), Version = 1 },
                TestContext.Current.CancellationToken));

        await using var verificationScope = provider.CreateAsyncScope();
        var survivor = Assert.IsType<OpenIddictMartenApplication>(await verificationScope.ServiceProvider
            .GetRequiredService<IOpenIddictApplicationStore<OpenIddictMartenApplication>>()
            .FindByIdAsync(application.Id.ToString("D"), TestContext.Current.CancellationToken));
        Assert.Equal("Current", survivor.DisplayName);
        Assert.True(survivor.Version > stale.Version);
    }

    private static async Task VerifyScopeConcurrencyAsync(ServiceProvider provider)
    {
        var persistedScope = new OpenIddictMartenScope
        {
            Name = $"concurrency-scope-{Guid.NewGuid():N}",
            DisplayName = "Before",
        };
        await using (var createScope = provider.CreateAsyncScope())
        {
            await createScope.ServiceProvider.GetRequiredService<
                IOpenIddictScopeStore<OpenIddictMartenScope>>()
                .CreateAsync(persistedScope, TestContext.Current.CancellationToken);
        }

        await using var staleScope = provider.CreateAsyncScope();
        await using var currentScope = provider.CreateAsyncScope();
        var staleStore = staleScope.ServiceProvider.GetRequiredService<
            IOpenIddictScopeStore<OpenIddictMartenScope>>();
        var currentStore = currentScope.ServiceProvider.GetRequiredService<
            IOpenIddictScopeStore<OpenIddictMartenScope>>();
        var stale = Assert.IsType<OpenIddictMartenScope>(await staleStore.FindByIdAsync(
            persistedScope.Id.ToString("D"),
            TestContext.Current.CancellationToken));
        var current = Assert.IsType<OpenIddictMartenScope>(await currentStore.FindByIdAsync(
            persistedScope.Id.ToString("D"),
            TestContext.Current.CancellationToken));

        current.DisplayName = "Current";
        await currentStore.UpdateAsync(current, TestContext.Current.CancellationToken);

        stale.DisplayName = "Stale";
        await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(async () =>
            await staleStore.UpdateAsync(stale, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(async () =>
            await staleStore.DeleteAsync(stale, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(async () =>
            await currentStore.UpdateAsync(
                new OpenIddictMartenScope { Id = Guid.NewGuid(), Version = 1 },
                TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(async () =>
            await currentStore.DeleteAsync(
                new OpenIddictMartenScope { Id = Guid.NewGuid(), Version = 1 },
                TestContext.Current.CancellationToken));

        await using var verificationScope = provider.CreateAsyncScope();
        var survivor = Assert.IsType<OpenIddictMartenScope>(await verificationScope.ServiceProvider
            .GetRequiredService<IOpenIddictScopeStore<OpenIddictMartenScope>>()
            .FindByIdAsync(persistedScope.Id.ToString("D"), TestContext.Current.CancellationToken));
        Assert.Equal("Current", survivor.DisplayName);
        Assert.True(survivor.Version > stale.Version);
    }

    private static async Task<ServiceProvider> CreateProviderAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMarten(options =>
        {
            options.Connection(Environment.GetEnvironmentVariable(ConnectionVariable)!);
            options.AutoCreateSchemaObjects = AutoCreate.CreateOrUpdate;
        });
        services.AddOpenIddict().AddCore(options => options.UseMarten());

        var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        await provider.GetRequiredService<IDocumentStore>()
            .Storage.ApplyAllConfiguredChangesToDatabaseAsync();
        return provider;
    }

    private static bool ContainsPostgreSqlUniqueViolation(Exception exception)
    {
        if (exception is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return true;
        }

        if (exception is AggregateException aggregate &&
            aggregate.InnerExceptions.Any(ContainsPostgreSqlUniqueViolation))
        {
            return true;
        }

        return exception.InnerException is not null &&
            ContainsPostgreSqlUniqueViolation(exception.InnerException);
    }
}
