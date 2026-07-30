using System.Security.Claims;
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

    [Fact(
        Skip = "Set INNOVORIUM_TEST_POSTGRES to run the disposable PostgreSQL integration test.",
        SkipUnless = nameof(HasPostgreSql))]
    public async Task IdentityManagersPersistEverySupportedUserAndRoleCapability()
    {
        await using var provider = await CreateRoleEnabledProviderAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var userId = $"manager-user-{suffix}";
        var roleId = $"manager-role-{suffix}";
        var userClaim = new Claim("permission", $"reports:{suffix}");
        var roleClaim = new Claim("permission", $"admin:{suffix}");
        var login = new UserLoginInfo("github", $"github-{suffix}", "GitHub");
        var credentialId = Guid.NewGuid().ToByteArray();

        await using (var scope = provider.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
            var role = new ApplicationRole { Id = roleId, Name = $"Admin-{suffix}" };
            var user = new ApplicationUser
            {
                Id = userId,
                UserName = $"ada-{suffix}",
                Email = $"ada-{suffix}@example.test",
            };

            AssertSucceeded(await roles.CreateAsync(role));
            AssertSucceeded(await roles.AddClaimAsync(role, roleClaim));
            AssertSucceeded(await users.CreateAsync(user));
            AssertSucceeded(await users.AddClaimAsync(user, userClaim));
            AssertSucceeded(await users.AddLoginAsync(user, login));
            AssertSucceeded(await users.SetAuthenticationTokenAsync(
                    user,
                    "customer-provider",
                    "refresh-token",
                    $"token-{suffix}")
                );
            AssertSucceeded(await users.ResetAuthenticatorKeyAsync(user));
            var recoveryCodes = Assert.IsAssignableFrom<IEnumerable<string>>(
                    await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 3))
                .ToArray();
            Assert.Equal(3, recoveryCodes.Length);
            AssertSucceeded(await users.AddOrUpdatePasskeyAsync(user, CreatePasskey(credentialId, "Laptop"))
                );
            AssertSucceeded(await users.AddToRoleAsync(user, role.Name!));
        }

        await using (var verificationScope = provider.CreateAsyncScope())
        {
            var users = verificationScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roles = verificationScope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
            var user = Assert.IsType<ApplicationUser>(await users.FindByIdAsync(userId));
            var role = Assert.IsType<ApplicationRole>(await roles.FindByIdAsync(roleId));

            Assert.Contains(
                await users.GetClaimsAsync(user),
                claim => claim.Type == userClaim.Type && claim.Value == userClaim.Value);
            Assert.Contains(
                (await users.GetUsersForClaimAsync(userClaim)),
                candidate => candidate.Id == userId);
            Assert.Equal(userId, (await users.FindByLoginAsync("github", login.ProviderKey))?.Id);
            Assert.Contains(
                await users.GetLoginsAsync(user),
                candidate => candidate.LoginProvider == "github" && candidate.ProviderKey == login.ProviderKey);
            Assert.Equal(
                $"token-{suffix}",
                await users.GetAuthenticationTokenAsync(user, "customer-provider", "refresh-token")
                    );
            Assert.False(string.IsNullOrWhiteSpace(await users.GetAuthenticatorKeyAsync(user)));
            Assert.Equal(3, await users.CountRecoveryCodesAsync(user));
            Assert.True(await users.IsInRoleAsync(user, role.Name!));
            Assert.Contains(role.Name!, await users.GetRolesAsync(user));
            Assert.Contains(
                await roles.GetClaimsAsync(role),
                claim => claim.Type == roleClaim.Type && claim.Value == roleClaim.Value);

            var passkey = Assert.Single(await users.GetPasskeysAsync(user));
            Assert.Equal("Laptop", passkey.Name);
            Assert.Equal(credentialId, passkey.CredentialId);
            Assert.Equal(userId, (await users.FindByPasskeyIdAsync(credentialId))?.Id);
            Assert.Equal("Laptop", (await users.GetPasskeyAsync(user, credentialId))?.Name);
        }
    }

    [Fact(
        Skip = "Set INNOVORIUM_TEST_POSTGRES to run the disposable PostgreSQL integration test.",
        SkipUnless = nameof(HasPostgreSql))]
    public async Task ManagerRacesReturnStandardDuplicateIdentityErrors()
    {
        await using var provider = await CreateRoleEnabledProviderAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var userName = $"race-{suffix}";
        var roleName = $"RaceRole-{suffix}";

        var userResults = await Task.WhenAll(
            CreateUserAsync(provider, $"race-user-a-{suffix}", userName),
            CreateUserAsync(provider, $"race-user-b-{suffix}", userName));
        Assert.Single(userResults, result => result.Succeeded);
        var duplicateUser = Assert.Single(userResults, result => !result.Succeeded);
        Assert.Equal("DuplicateUserName", Assert.Single(duplicateUser.Errors).Code);

        var roleResults = await Task.WhenAll(
            CreateRoleAsync(provider, $"race-role-a-{suffix}", roleName),
            CreateRoleAsync(provider, $"race-role-b-{suffix}", roleName));
        Assert.Single(roleResults, result => result.Succeeded);
        var duplicateRole = Assert.Single(roleResults, result => !result.Succeeded);
        Assert.Equal("DuplicateRoleName", Assert.Single(duplicateRole.Errors).Code);
    }

    [Fact(
        Skip = "Set INNOVORIUM_TEST_POSTGRES to run the disposable PostgreSQL integration test.",
        SkipUnless = nameof(HasPostgreSql))]
    public async Task ManagerDeletesAtomicallyCleanUserAndRoleRelationships()
    {
        await using var provider = await CreateRoleEnabledProviderAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var userId = $"cascade-user-{suffix}";
        var roleId = $"cascade-role-{suffix}";
        var roleName = $"CascadeRole-{suffix}";
        var loginKey = $"cascade-login-{suffix}";
        var credentialId = Guid.NewGuid().ToByteArray();

        await using (var scope = provider.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
            var user = new ApplicationUser
            {
                Id = userId,
                UserName = $"cascade-{suffix}",
                Email = $"cascade-{suffix}@example.test",
            };
            var role = new ApplicationRole { Id = roleId, Name = roleName };
            AssertSucceeded(await roles.CreateAsync(role));
            AssertSucceeded(await roles.AddClaimAsync(role, new Claim("role", suffix)));
            AssertSucceeded(await users.CreateAsync(user));
            AssertSucceeded(await users.AddClaimAsync(user, new Claim("user", suffix)));
            AssertSucceeded(await users.AddLoginAsync(
                    user,
                    new UserLoginInfo("github", loginKey, "GitHub"))
                );
            AssertSucceeded(await users.SetAuthenticationTokenAsync(user, "provider", "token", suffix)
                );
            AssertSucceeded(await users.AddOrUpdatePasskeyAsync(user, CreatePasskey(credentialId, "Phone"))
                );
            AssertSucceeded(await users.AddToRoleAsync(user, roleName));
            AssertSucceeded(await roles.DeleteAsync(role));
        }

        var documentStore = provider.GetRequiredService<IDocumentStore>();
        await using (var query = documentStore.QuerySession())
        {
            Assert.Empty(await query.Query<MartenIdentityRoleClaim<ApplicationRole>>()
                .Where(document => document.RoleId == roleId)
                .ToListAsync(TestContext.Current.CancellationToken)
                );
            Assert.Empty(await query.Query<MartenIdentityUserRole>()
                .Where(document => document.RoleId == roleId)
                .ToListAsync(TestContext.Current.CancellationToken)
                );
        }

        await using (var deleteScope = provider.CreateAsyncScope())
        {
            var users = deleteScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = Assert.IsType<ApplicationUser>(await users.FindByIdAsync(userId));
            Assert.Empty(await users.GetRolesAsync(user));
            AssertSucceeded(await users.DeleteAsync(user));
        }

        await using (var query = documentStore.QuerySession())
        {
            Assert.Null(await query.LoadAsync<ApplicationUser>(userId, TestContext.Current.CancellationToken)
                );
            Assert.Empty(await query.Query<MartenIdentityUserClaim<ApplicationUser>>()
                .Where(document => document.UserId == userId)
                .ToListAsync(TestContext.Current.CancellationToken)
                );
            Assert.Empty(await query.Query<MartenIdentityUserLogin<ApplicationUser>>()
                .Where(document => document.UserId == userId)
                .ToListAsync(TestContext.Current.CancellationToken)
                );
            Assert.Empty(await query.Query<MartenIdentityUserToken<ApplicationUser>>()
                .Where(document => document.UserId == userId)
                .ToListAsync(TestContext.Current.CancellationToken)
                );
            Assert.Empty(await query.Query<MartenIdentityUserPasskey<ApplicationUser>>()
                .Where(document => document.UserId == userId)
                .ToListAsync(TestContext.Current.CancellationToken)
                );
            Assert.Empty(await query.Query<MartenIdentityUserRole>()
                .Where(document => document.UserId == userId)
                .ToListAsync(TestContext.Current.CancellationToken)
                );
        }

        await using var verificationScope = provider.CreateAsyncScope();
        var verification = verificationScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Null(await verification.FindByLoginAsync("github", loginKey));
        Assert.Null(await verification.FindByPasskeyIdAsync(credentialId));
    }

    [Fact(
        Skip = "Set INNOVORIUM_TEST_POSTGRES to run the disposable PostgreSQL integration test.",
        SkipUnless = nameof(HasPostgreSql))]
    public async Task FailedManagerOperationsCannotLeakBufferedRelationshipsIntoLaterUpdates()
    {
        await using var provider = await CreateRoleEnabledProviderAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var userId = $"buffer-user-{suffix}";
        var rejectedClaim = new Claim("rejected", suffix);
        var rejectedRoleClaim = new Claim("rejected-role", suffix);
        var newRoleName = $"NewRole-{suffix}";
        var existingRoleName = $"ExistingRole-{suffix}";

        await using (var scope = provider.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
            var user = new ApplicationUser
            {
                Id = userId,
                UserName = $"buffer-{suffix}",
                Email = $"buffer-{suffix}@example.test",
            };
            AssertSucceeded(await users.CreateAsync(user));
            AssertSucceeded(await roles.CreateAsync(new ApplicationRole
            {
                Id = $"new-role-{suffix}",
                Name = newRoleName,
            }));
            var existingRole = new ApplicationRole
            {
                Id = $"existing-role-{suffix}",
                Name = existingRoleName,
            };
            AssertSucceeded(await roles.CreateAsync(existingRole));
            AssertSucceeded(await users.AddToRoleAsync(user, existingRoleName));

            user.UserName = null;
            var invalidClaim = await users.AddClaimAsync(user, rejectedClaim);
            Assert.False(invalidClaim.Succeeded);

            user.UserName = $"buffer-{suffix}";
            user.PhoneNumber = "+15550000001";
            AssertSucceeded(await users.UpdateAsync(user));

            var partialRoleBatch = await users.AddToRolesAsync(user, [newRoleName, existingRoleName]);
            Assert.False(partialRoleBatch.Succeeded);
            user.PhoneNumber = "+15550000002";
            AssertSucceeded(await users.UpdateAsync(user));

            existingRole.Name = null;
            var invalidRoleClaim = await roles.AddClaimAsync(existingRole, rejectedRoleClaim);
            Assert.False(invalidRoleClaim.Succeeded);
            existingRole.Name = existingRoleName;
            AssertSucceeded(await roles.UpdateAsync(existingRole));
        }

        await using var verificationScope = provider.CreateAsyncScope();
        var verification = verificationScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var persisted = Assert.IsType<ApplicationUser>(await verification.FindByIdAsync(userId));
        Assert.DoesNotContain(
            await verification.GetClaimsAsync(persisted),
            claim => claim.Type == rejectedClaim.Type && claim.Value == rejectedClaim.Value);
        Assert.False(await verification.IsInRoleAsync(persisted, newRoleName));
        Assert.True(await verification.IsInRoleAsync(persisted, existingRoleName));
        Assert.Equal("+15550000002", persisted.PhoneNumber);
        var persistedRole = Assert.IsType<ApplicationRole>(await verificationScope.ServiceProvider
            .GetRequiredService<RoleManager<ApplicationRole>>()
            .FindByNameAsync(existingRoleName));
        Assert.DoesNotContain(
            await verificationScope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>()
                .GetClaimsAsync(persistedRole),
            claim => claim.Type == rejectedRoleClaim.Type && claim.Value == rejectedRoleClaim.Value);
    }

    [Fact(
        Skip = "Set INNOVORIUM_TEST_POSTGRES to run the disposable PostgreSQL integration test.",
        SkipUnless = nameof(HasPostgreSql))]
    public async Task RoleDeletionRacingMembershipInsertCannotLeaveAnOrphanMembership()
    {
        await using var provider = await CreateRoleEnabledProviderAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var userId = $"fk-user-{suffix}";
        var roleId = $"fk-role-{suffix}";
        var roleName = $"FkRole-{suffix}";

        await using (var setupScope = provider.CreateAsyncScope())
        {
            var users = setupScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roles = setupScope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
            AssertSucceeded(await users.CreateAsync(new ApplicationUser
            {
                Id = userId,
                UserName = $"fk-{suffix}",
                Email = $"fk-{suffix}@example.test",
            }));
            AssertSucceeded(await roles.CreateAsync(new ApplicationRole { Id = roleId, Name = roleName }));
        }

        await using (var membershipScope = provider.CreateAsyncScope())
        await using (var deletionScope = provider.CreateAsyncScope())
        {
            var membershipStore = Assert.IsAssignableFrom<IUserRoleStore<ApplicationUser>>(
                membershipScope.ServiceProvider.GetRequiredService<IUserStore<ApplicationUser>>());
            var deletionRoles = deletionScope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
            var user = Assert.IsType<ApplicationUser>(await membershipStore.FindByIdAsync(
                userId,
                TestContext.Current.CancellationToken));
            var role = Assert.IsType<ApplicationRole>(await deletionRoles.FindByIdAsync(roleId));

            await membershipStore.AddToRoleAsync(user, role.NormalizedName!, TestContext.Current.CancellationToken);
            AssertSucceeded(await deletionRoles.DeleteAsync(role));

            var racedUpdate = await membershipStore.UpdateAsync(user, TestContext.Current.CancellationToken);
            Assert.False(racedUpdate.Succeeded);
            Assert.Equal("RoleNotFound", Assert.Single(racedUpdate.Errors).Code);
        }

        await using (var recreateScope = provider.CreateAsyncScope())
        {
            var roles = recreateScope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
            AssertSucceeded(await roles.CreateAsync(new ApplicationRole { Id = roleId, Name = roleName }));
        }

        await using var verificationScope = provider.CreateAsyncScope();
        var verification = verificationScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var persisted = Assert.IsType<ApplicationUser>(await verification.FindByIdAsync(userId));
        Assert.False(await verification.IsInRoleAsync(persisted, roleName));
    }

    [Fact(
        Skip = "Set INNOVORIUM_TEST_POSTGRES to run the disposable PostgreSQL integration test.",
        SkipUnless = nameof(HasPostgreSql))]
    public async Task PasskeyCredentialCannotBeTakenFromAnotherUserOrLeakPendingState()
    {
        await using var provider = await CreateRoleEnabledProviderAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var ownerId = $"passkey-owner-{suffix}";
        var secondUserId = $"passkey-second-{suffix}";
        var credentialId = Guid.NewGuid().ToByteArray();
        var original = CreatePasskey(credentialId, "Owner key", [1, 3, 5, 7]);
        var replacement = CreatePasskey(credentialId, "Replacement", [2, 4, 6, 8]);

        await using (var scope = provider.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var owner = new ApplicationUser
            {
                Id = ownerId,
                UserName = $"passkey-owner-{suffix}",
                Email = $"passkey-owner-{suffix}@example.test",
            };
            var secondUser = new ApplicationUser
            {
                Id = secondUserId,
                UserName = $"passkey-second-{suffix}",
                Email = $"passkey-second-{suffix}@example.test",
            };
            AssertSucceeded(await users.CreateAsync(owner));
            AssertSucceeded(await users.CreateAsync(secondUser));
            AssertSucceeded(await users.AddOrUpdatePasskeyAsync(owner, original));

            var rejected = await users.AddOrUpdatePasskeyAsync(secondUser, replacement);
            Assert.False(rejected.Succeeded);
            Assert.Equal("DuplicatePasskey", Assert.Single(rejected.Errors).Code);

            AssertSucceeded(await users.SetPhoneNumberAsync(secondUser, "+15550000004"));
        }

        await using var verificationScope = provider.CreateAsyncScope();
        var verification = verificationScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var ownerUser = Assert.IsType<ApplicationUser>(await verification.FindByIdAsync(ownerId));
        var secondUserVerification = Assert.IsType<ApplicationUser>(
            await verification.FindByIdAsync(secondUserId));
        var persisted = Assert.Single(await verification.GetPasskeysAsync(ownerUser));
        Assert.Equal("Owner key", persisted.Name);
        Assert.Equal(original.PublicKey, persisted.PublicKey);
        Assert.Equal(original.SignCount, persisted.SignCount);
        Assert.Equal(ownerId, (await verification.FindByPasskeyIdAsync(credentialId))?.Id);
        Assert.Null(await verification.GetPasskeyAsync(secondUserVerification, credentialId));
        Assert.Empty(await verification.GetPasskeysAsync(secondUserVerification));
        Assert.Equal("+15550000004", secondUserVerification.PhoneNumber);
    }

    [Fact(
        Skip = "Set INNOVORIUM_TEST_POSTGRES to run the disposable PostgreSQL integration test.",
        SkipUnless = nameof(HasPostgreSql))]
    public async Task ConcurrentPasskeyRegistrationHasExactlyOneOwnerWithoutOverwrite()
    {
        await using var provider = await CreateRoleEnabledProviderAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var firstUserId = $"passkey-race-first-{suffix}";
        var secondUserId = $"passkey-race-second-{suffix}";
        var credentialId = Guid.NewGuid().ToByteArray();
        var firstPasskey = CreatePasskey(credentialId, "First key", [1, 1, 1, 1]);
        var secondPasskey = CreatePasskey(credentialId, "Second key", [2, 2, 2, 2]);

        await using (var setupScope = provider.CreateAsyncScope())
        {
            var users = setupScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            AssertSucceeded(await users.CreateAsync(new ApplicationUser
            {
                Id = firstUserId,
                UserName = $"passkey-race-first-{suffix}",
                Email = $"passkey-race-first-{suffix}@example.test",
            }));
            AssertSucceeded(await users.CreateAsync(new ApplicationUser
            {
                Id = secondUserId,
                UserName = $"passkey-race-second-{suffix}",
                Email = $"passkey-race-second-{suffix}@example.test",
            }));
        }

        await using var firstScope = provider.CreateAsyncScope();
        await using var secondScope = provider.CreateAsyncScope();
        var firstStore = firstScope.ServiceProvider.GetRequiredService<IUserStore<ApplicationUser>>();
        var secondStore = secondScope.ServiceProvider.GetRequiredService<IUserStore<ApplicationUser>>();
        var firstUser = Assert.IsType<ApplicationUser>(await firstStore.FindByIdAsync(
            firstUserId,
            TestContext.Current.CancellationToken));
        var secondUser = Assert.IsType<ApplicationUser>(await secondStore.FindByIdAsync(
            secondUserId,
            TestContext.Current.CancellationToken));
        await Assert.IsAssignableFrom<IUserPasskeyStore<ApplicationUser>>(firstStore)
            .AddOrUpdatePasskeyAsync(firstUser, firstPasskey, TestContext.Current.CancellationToken);
        await Assert.IsAssignableFrom<IUserPasskeyStore<ApplicationUser>>(secondStore)
            .AddOrUpdatePasskeyAsync(secondUser, secondPasskey, TestContext.Current.CancellationToken);

        var results = await Task.WhenAll(
            firstStore.UpdateAsync(firstUser, TestContext.Current.CancellationToken),
            secondStore.UpdateAsync(secondUser, TestContext.Current.CancellationToken));
        var winnerIndex = Assert.Single(Enumerable.Range(0, 2), index => results[index].Succeeded);
        var loserIndex = Assert.Single(Enumerable.Range(0, 2), index => !results[index].Succeeded);
        Assert.Equal("DuplicatePasskey", Assert.Single(results[loserIndex].Errors).Code);

        var expectedOwnerId = winnerIndex == 0 ? firstUserId : secondUserId;
        var expectedPasskey = winnerIndex == 0 ? firstPasskey : secondPasskey;
        await using var verificationScope = provider.CreateAsyncScope();
        var verification = verificationScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var resolvedOwner = Assert.IsType<ApplicationUser>(
            await verification.FindByPasskeyIdAsync(credentialId));
        Assert.Equal(expectedOwnerId, resolvedOwner.Id);
        var persisted = Assert.Single(await verification.GetPasskeysAsync(resolvedOwner));
        Assert.Equal(expectedPasskey.Name, persisted.Name);
        Assert.Equal(expectedPasskey.PublicKey, persisted.PublicKey);
        var loser = Assert.IsType<ApplicationUser>(await verification.FindByIdAsync(
            loserIndex == 0 ? firstUserId : secondUserId));
        Assert.Empty(await verification.GetPasskeysAsync(loser));
    }

    private static async Task<ServiceProvider> CreateRoleEnabledProviderAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMarten(options =>
        {
            options.Connection(Environment.GetEnvironmentVariable(ConnectionVariable)!);
            options.AutoCreateSchemaObjects = AutoCreate.CreateOrUpdate;
        });
        services.AddIdentityCore<ApplicationUser>(options => options.User.RequireUniqueEmail = true)
            .AddRoles<ApplicationRole>()
            .AddMartenStores();
        var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<IDocumentStore>()
            .Storage.ApplyAllConfiguredChangesToDatabaseAsync()
            ;
        return provider;
    }

    private static async Task<IdentityResult> CreateUserAsync(
        IServiceProvider provider,
        string id,
        string userName)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
            .CreateAsync(new ApplicationUser
            {
                Id = id,
                UserName = userName,
                Email = $"{id}@example.test",
            })
            ;
    }

    private static async Task<IdentityResult> CreateRoleAsync(
        IServiceProvider provider,
        string id,
        string roleName)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>()
            .CreateAsync(new ApplicationRole { Id = id, Name = roleName })
            ;
    }

    private static UserPasskeyInfo CreatePasskey(byte[] credentialId, string name, byte[]? publicKey = null) =>
        new(
            credentialId,
            publicKey ?? [1, 2, 3, 4],
            DateTimeOffset.UtcNow,
            7,
            ["internal", "hybrid"],
            isUserVerified: true,
            isBackupEligible: true,
            isBackedUp: false,
            [5, 6],
            [7, 8])
        {
            Name = name,
        };

    private static void AssertSucceeded(IdentityResult result) =>
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Description}")));

    private sealed class ApplicationUser : MartenIdentityUser;

    private sealed class ApplicationRole : MartenIdentityRole;
}
