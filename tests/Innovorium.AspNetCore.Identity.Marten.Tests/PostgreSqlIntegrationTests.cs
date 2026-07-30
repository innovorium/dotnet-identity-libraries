using JasperFx;
using Marten;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Innovorium.AspNetCore.Identity.Marten.Tests;

public sealed class PostgreSqlIntegrationTests
{
    private const string ConnectionVariable = "INNOVORIUM_TEST_POSTGRES";

    public static bool HasPostgreSql =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable));

    [Fact(
        Skip = "Set INNOVORIUM_TEST_POSTGRES to run the disposable PostgreSQL integration test.",
        SkipUnless = nameof(HasPostgreSql))]
    public async Task CustomerRegistrationCreatesOneMappingAndVersionGuardsUpdateAndDelete()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMarten(options =>
        {
            options.Connection(Environment.GetEnvironmentVariable(ConnectionVariable)!);
            options.AutoCreateSchemaObjects = AutoCreate.CreateOrUpdate;
        });

        var identity = services.AddIdentityCore<ApplicationUser>();
        identity.AddMartenStores();
        identity.AddMartenStores();

        await using var provider = services.BuildServiceProvider();
        var documentStore = provider.GetRequiredService<IDocumentStore>();
        await documentStore.Storage.ApplyAllConfiguredChangesToDatabaseAsync();

        var mapping = documentStore.Options.FindOrResolveDocumentType(typeof(ApplicationUser));
        Assert.Equal("identity_user", mapping.Alias);
        Assert.True(mapping.UseOptimisticConcurrency);
        Assert.Single(mapping.Indexes, index =>
            index.Name == IdentityBuilderExtensions.NormalizedUserNameIndex);
        Assert.Single(mapping.Indexes, index =>
            index.Name == IdentityBuilderExtensions.NormalizedEmailIndex);

        await using (var query = documentStore.QuerySession())
        {
            var databaseIndexes = await query.QueryAsync<string>(
                "select indexname from pg_indexes where schemaname = ? and tablename = ?",
                TestContext.Current.CancellationToken,
                mapping.TableName.Schema,
                mapping.TableName.Name);

            Assert.Contains(IdentityBuilderExtensions.NormalizedUserNameIndex, databaseIndexes);
            Assert.Contains(IdentityBuilderExtensions.NormalizedEmailIndex, databaseIndexes);
        }

        var userId = $"identity-concurrency-{Guid.NewGuid():N}";
        await using (var createScope = provider.CreateAsyncScope())
        {
            var store = createScope.ServiceProvider.GetRequiredService<IUserStore<ApplicationUser>>();
            var created = await store.CreateAsync(
                new ApplicationUser
                {
                    Id = userId,
                    UserName = "ada",
                    NormalizedUserName = $"ADA-{userId}",
                    Email = "ada-before@example.test",
                    NormalizedEmail = $"ADA-BEFORE-{userId}@EXAMPLE.TEST",
                },
                TestContext.Current.CancellationToken);

            Assert.True(created.Succeeded);
        }

        await using var staleScope = provider.CreateAsyncScope();
        await using var currentScope = provider.CreateAsyncScope();
        var staleStore = staleScope.ServiceProvider.GetRequiredService<IUserStore<ApplicationUser>>();
        var currentStore = currentScope.ServiceProvider.GetRequiredService<IUserStore<ApplicationUser>>();
        var stale = Assert.IsType<ApplicationUser>(
            await staleStore.FindByIdAsync(userId, TestContext.Current.CancellationToken));
        var current = Assert.IsType<ApplicationUser>(
            await currentStore.FindByIdAsync(userId, TestContext.Current.CancellationToken));

        current.Email = "ada-after@example.test";
        current.NormalizedEmail = $"ADA-AFTER-{userId}@EXAMPLE.TEST";
        var updated = await currentStore.UpdateAsync(current, TestContext.Current.CancellationToken);
        Assert.True(updated.Succeeded);

        var staleDelete = await staleStore.DeleteAsync(stale, TestContext.Current.CancellationToken);
        var concurrencyError = Assert.Single(staleDelete.Errors);
        Assert.False(staleDelete.Succeeded);
        Assert.Equal("ConcurrencyFailure", concurrencyError.Code);

        await using (var verification = documentStore.QuerySession())
        {
            var preserved = Assert.IsType<ApplicationUser>(
                await verification.LoadAsync<ApplicationUser>(userId, TestContext.Current.CancellationToken));
            Assert.Equal("ada-after@example.test", preserved.Email);
            Assert.NotEqual(stale.Version, preserved.Version);
        }

        await using var deleteScope = provider.CreateAsyncScope();
        var deleteStore = deleteScope.ServiceProvider.GetRequiredService<IUserStore<ApplicationUser>>();
        var latest = Assert.IsType<ApplicationUser>(
            await deleteStore.FindByIdAsync(userId, TestContext.Current.CancellationToken));
        var deleted = await deleteStore.DeleteAsync(latest, TestContext.Current.CancellationToken);
        Assert.True(deleted.Succeeded);

        await using var finalVerification = documentStore.QuerySession();
        Assert.Null(await finalVerification.LoadAsync<ApplicationUser>(
            userId,
            TestContext.Current.CancellationToken));
    }

    private sealed class ApplicationUser : MartenIdentityUser;
}
