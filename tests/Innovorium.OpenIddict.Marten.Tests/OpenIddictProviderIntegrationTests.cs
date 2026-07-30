using System.Collections.Immutable;
using JasperFx;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OpenIddict.Abstractions;
using Xunit;

namespace Innovorium.OpenIddict.Marten.Tests;

[Collection("OpenIddict PostgreSQL")]
public sealed class OpenIddictProviderIntegrationTests
{
    private const string ConnectionVariable = "INNOVORIUM_TEST_POSTGRES";

    public static bool HasPostgreSql =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable));

    [Fact(
        Skip = "Set INNOVORIUM_TEST_POSTGRES to run the disposable PostgreSQL integration test.",
        SkipUnless = nameof(HasPostgreSql))]
    public async Task CustomerManagersSupportAllFourStoresLookupsAndUniqueness()
    {
        await using var provider = await CreateProviderAsync();
        await using var scope = provider.CreateAsyncScope();
        var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var authorizations = scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
        var scopes = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
        var tokens = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
        var applicationStore = scope.ServiceProvider.GetRequiredService<
            IOpenIddictApplicationStore<OpenIddictMartenApplication>>();
        var authorizationStore = scope.ServiceProvider.GetRequiredService<
            IOpenIddictAuthorizationStore<OpenIddictMartenAuthorization>>();
        var tokenStore = scope.ServiceProvider.GetRequiredService<
            IOpenIddictTokenStore<OpenIddictMartenToken>>();
        var scopeStore = scope.ServiceProvider.GetRequiredService<
            IOpenIddictScopeStore<OpenIddictMartenScope>>();
        var suffix = Guid.NewGuid().ToString("N");
        var clientId = $"manager-client-{suffix}";
        var scopeName = $"profile-{suffix}";
        var redirectUri = $"https://customer.example.test/callback/{suffix}";
        var resource = $"customer-api-{suffix}";

        var application = await applications.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientType = OpenIddictConstants.ClientTypes.Public,
            DisplayName = "Manager client",
            RedirectUris = { new Uri(redirectUri) },
        }, TestContext.Current.CancellationToken);
        var applicationId = Assert.IsType<string>(await applications.GetIdAsync(
            application,
            TestContext.Current.CancellationToken));
        await scopes.CreateAsync(new OpenIddictScopeDescriptor
        {
            Name = scopeName,
            DisplayName = "Customer profile",
            Resources = { resource },
        }, TestContext.Current.CancellationToken);

        Assert.NotNull(await applications.FindByClientIdAsync(clientId, TestContext.Current.CancellationToken));
        Assert.NotNull(await scopes.FindByNameAsync(scopeName, TestContext.Current.CancellationToken));
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
        var uniqueViolation = await Assert.ThrowsAnyAsync<Exception>(async () =>
            await applicationStore.CreateAsync(duplicate, TestContext.Current.CancellationToken));
        Assert.True(ContainsPostgreSqlUniqueViolation(uniqueViolation));

        var authorization = await authorizations.CreateAsync(new OpenIddictAuthorizationDescriptor
        {
            ApplicationId = applicationId,
            CreationDate = DateTimeOffset.UtcNow,
            Status = OpenIddictConstants.Statuses.Valid,
            Subject = $"manager-subject-{suffix}",
            Type = OpenIddictConstants.AuthorizationTypes.Permanent,
            Scopes = { "openid", "profile" },
        }, TestContext.Current.CancellationToken);
        var authorizationId = Assert.IsType<string>(await authorizations.GetIdAsync(
            authorization,
            TestContext.Current.CancellationToken));

        var token = await tokens.CreateAsync(new OpenIddictTokenDescriptor
        {
            ApplicationId = applicationId,
            AuthorizationId = authorizationId,
            CreationDate = DateTimeOffset.UtcNow,
            ExpirationDate = DateTimeOffset.UtcNow.AddHours(1),
            Payload = "protected-payload",
            ReferenceId = $"reference-{suffix}",
            Status = OpenIddictConstants.Statuses.Valid,
            Subject = $"manager-subject-{suffix}",
            Type = OpenIddictConstants.TokenTypeHints.AccessToken,
        }, TestContext.Current.CancellationToken);
        var tokenId = Assert.IsType<string>(await tokens.GetIdAsync(
            token,
            TestContext.Current.CancellationToken));

        Assert.Same(authorization, await authorizations.FindByIdAsync(
            authorizationId,
            TestContext.Current.CancellationToken));
        Assert.Same(token, await tokens.FindByIdAsync(tokenId, TestContext.Current.CancellationToken));
        Assert.Same(token, await tokens.FindByReferenceIdAsync(
            $"reference-{suffix}",
            TestContext.Current.CancellationToken));
        Assert.Single(await authorizationStore.FindAsync(
            $"manager-subject-{suffix}",
            applicationId,
            OpenIddictConstants.Statuses.Valid,
            OpenIddictConstants.AuthorizationTypes.Permanent,
            ImmutableArray.Create("openid", "profile"),
            TestContext.Current.CancellationToken).ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await authorizationStore.FindByApplicationIdAsync(
            applicationId,
            TestContext.Current.CancellationToken).ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await authorizationStore.FindBySubjectAsync(
            $"manager-subject-{suffix}",
            TestContext.Current.CancellationToken).ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await tokenStore.FindAsync(
            $"manager-subject-{suffix}",
            applicationId,
            OpenIddictConstants.Statuses.Valid,
            OpenIddictConstants.TokenTypeHints.AccessToken,
            TestContext.Current.CancellationToken).ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await tokenStore.FindByApplicationIdAsync(
            applicationId,
            TestContext.Current.CancellationToken).ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await tokenStore.FindByAuthorizationIdAsync(
            authorizationId,
            TestContext.Current.CancellationToken).ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await tokenStore.FindBySubjectAsync(
            $"manager-subject-{suffix}",
            TestContext.Current.CancellationToken).ToListAsync(TestContext.Current.CancellationToken));

        var typedAuthorization = Assert.IsType<OpenIddictMartenAuthorization>(authorization);
        typedAuthorization.Status = OpenIddictConstants.Statuses.Revoked;
        await authorizations.UpdateAsync(typedAuthorization, TestContext.Current.CancellationToken);
        var typedToken = Assert.IsType<OpenIddictMartenToken>(token);
        typedToken.Status = OpenIddictConstants.Statuses.Revoked;
        await tokens.UpdateAsync(typedToken, TestContext.Current.CancellationToken);

        await tokens.DeleteAsync(token, TestContext.Current.CancellationToken);
        await authorizations.DeleteAsync(authorization, TestContext.Current.CancellationToken);
        await applications.DeleteAsync(application, TestContext.Current.CancellationToken);
        Assert.Null(await tokens.FindByIdAsync(tokenId, TestContext.Current.CancellationToken));
        Assert.Null(await authorizations.FindByIdAsync(authorizationId, TestContext.Current.CancellationToken));
    }

    [Fact(
        Skip = "Set INNOVORIUM_TEST_POSTGRES to run the disposable PostgreSQL integration test.",
        SkipUnless = nameof(HasPostgreSql))]
    public async Task AllStoresRejectStaleAndMissingMutationsAndCascadesReadFreshState()
    {
        await using var provider = await CreateProviderAsync();
        await VerifyApplicationConcurrencyAsync(provider);
        await VerifyScopeConcurrencyAsync(provider);
        var application = new OpenIddictMartenApplication
        {
            ClientId = $"cascade-client-{Guid.NewGuid():N}",
        };
        var authorization = new OpenIddictMartenAuthorization
        {
            ApplicationId = application.Id,
            Status = OpenIddictConstants.Statuses.Valid,
            Subject = "cascade-subject",
            Type = OpenIddictConstants.AuthorizationTypes.Permanent,
        };
        var token = new OpenIddictMartenToken
        {
            ApplicationId = application.Id,
            AuthorizationId = authorization.Id,
            Status = OpenIddictConstants.Statuses.Valid,
            Subject = "cascade-subject",
            Type = OpenIddictConstants.TokenTypeHints.AccessToken,
        };
        await InsertGraphAsync(provider, application, authorization, token);

        await using var staleScope = provider.CreateAsyncScope();
        await using var currentScope = provider.CreateAsyncScope();
        var staleAuthorizationStore = staleScope.ServiceProvider.GetRequiredService<
            IOpenIddictAuthorizationStore<OpenIddictMartenAuthorization>>();
        var currentAuthorizationStore = currentScope.ServiceProvider.GetRequiredService<
            IOpenIddictAuthorizationStore<OpenIddictMartenAuthorization>>();
        var staleAuthorization = Assert.IsType<OpenIddictMartenAuthorization>(
            await staleAuthorizationStore.FindByIdAsync(
                authorization.Id.ToString("D"),
                TestContext.Current.CancellationToken));
        var currentAuthorization = Assert.IsType<OpenIddictMartenAuthorization>(
            await currentAuthorizationStore.FindByIdAsync(
                authorization.Id.ToString("D"),
                TestContext.Current.CancellationToken));
        currentAuthorization.Subject = "current";
        await currentAuthorizationStore.UpdateAsync(currentAuthorization, TestContext.Current.CancellationToken);
        staleAuthorization.Subject = "stale";
        await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(async () =>
            await staleAuthorizationStore.UpdateAsync(
                staleAuthorization,
                TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(async () =>
            await staleAuthorizationStore.DeleteAsync(
                staleAuthorization,
                TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(async () =>
            await currentAuthorizationStore.UpdateAsync(
                new OpenIddictMartenAuthorization { Id = Guid.NewGuid(), Version = 1 },
                TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(async () =>
            await currentAuthorizationStore.DeleteAsync(
                new OpenIddictMartenAuthorization { Id = Guid.NewGuid(), Version = 1 },
                TestContext.Current.CancellationToken));

        await using (var staleTokenScope = provider.CreateAsyncScope())
        await using (var currentTokenScope = provider.CreateAsyncScope())
        {
            var staleTokenStore = staleTokenScope.ServiceProvider.GetRequiredService<
                IOpenIddictTokenStore<OpenIddictMartenToken>>();
            var currentTokenStore = currentTokenScope.ServiceProvider.GetRequiredService<
                IOpenIddictTokenStore<OpenIddictMartenToken>>();
            var staleToken = Assert.IsType<OpenIddictMartenToken>(await staleTokenStore.FindByIdAsync(
                token.Id.ToString("D"),
                TestContext.Current.CancellationToken));
            var currentToken = Assert.IsType<OpenIddictMartenToken>(await currentTokenStore.FindByIdAsync(
                token.Id.ToString("D"),
                TestContext.Current.CancellationToken));
            currentToken.Subject = "current-token";
            await currentTokenStore.UpdateAsync(currentToken, TestContext.Current.CancellationToken);
            staleToken.Subject = "stale-token";
            await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(async () =>
                await staleTokenStore.UpdateAsync(staleToken, TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(async () =>
                await staleTokenStore.DeleteAsync(staleToken, TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(async () =>
                await currentTokenStore.UpdateAsync(
                    new OpenIddictMartenToken { Id = Guid.NewGuid(), Version = 1 },
                    TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<OpenIddictExceptions.ConcurrencyException>(async () =>
                await currentTokenStore.DeleteAsync(
                    new OpenIddictMartenToken { Id = Guid.NewGuid(), Version = 1 },
                    TestContext.Current.CancellationToken));
        }

        await using (var deleteScope = provider.CreateAsyncScope())
        {
            var store = deleteScope.ServiceProvider.GetRequiredService<
                IOpenIddictAuthorizationStore<OpenIddictMartenAuthorization>>();
            var value = Assert.IsType<OpenIddictMartenAuthorization>(await store.FindByIdAsync(
                authorization.Id.ToString("D"),
                TestContext.Current.CancellationToken));
            await store.DeleteAsync(value, TestContext.Current.CancellationToken);
        }

        await using (var verifyAuthorizationCascade = provider.CreateAsyncScope())
        {
            var store = verifyAuthorizationCascade.ServiceProvider.GetRequiredService<
                IOpenIddictTokenStore<OpenIddictMartenToken>>();
            Assert.Null(await store.FindByIdAsync(token.Id.ToString("D"), TestContext.Current.CancellationToken));
        }

        var cascadingApplication = new OpenIddictMartenApplication
        {
            ClientId = $"application-cascade-{Guid.NewGuid():N}",
        };
        var cascadingAuthorization = new OpenIddictMartenAuthorization
        {
            ApplicationId = cascadingApplication.Id,
        };
        var cascadingToken = new OpenIddictMartenToken
        {
            ApplicationId = cascadingApplication.Id,
            AuthorizationId = cascadingAuthorization.Id,
        };
        await InsertGraphAsync(provider, cascadingApplication, cascadingAuthorization, cascadingToken);

        await using (var deleteScope = provider.CreateAsyncScope())
        {
            var store = deleteScope.ServiceProvider.GetRequiredService<
                IOpenIddictApplicationStore<OpenIddictMartenApplication>>();
            var value = Assert.IsType<OpenIddictMartenApplication>(await store.FindByIdAsync(
                cascadingApplication.Id.ToString("D"),
                TestContext.Current.CancellationToken));
            await store.DeleteAsync(value, TestContext.Current.CancellationToken);
        }

        await using var verificationScope = provider.CreateAsyncScope();
        Assert.Null(await verificationScope.ServiceProvider.GetRequiredService<
            IOpenIddictAuthorizationStore<OpenIddictMartenAuthorization>>()
            .FindByIdAsync(cascadingAuthorization.Id.ToString("D"), TestContext.Current.CancellationToken));
        Assert.Null(await verificationScope.ServiceProvider.GetRequiredService<
            IOpenIddictTokenStore<OpenIddictMartenToken>>()
            .FindByIdAsync(cascadingToken.Id.ToString("D"), TestContext.Current.CancellationToken));
    }

    [Fact(
        Skip = "Set INNOVORIUM_TEST_POSTGRES to run the disposable PostgreSQL integration test.",
        SkipUnless = nameof(HasPostgreSql))]
    public async Task RevokeAndPruneMatchOpenIddictTruthTablesAndReturnExactCounts()
    {
        var now = new DateTimeOffset(2030, 7, 30, 10, 0, 0, TimeSpan.Zero);
        var old = now.AddYears(-2);
        var threshold = now.AddYears(-1);
        await using var provider = await CreateProviderAsync(new FixedTimeProvider(now));
        var application = new OpenIddictMartenApplication
        {
            ClientId = $"maintenance-client-{Guid.NewGuid():N}",
        };
        var relationApplication = new OpenIddictMartenApplication
        {
            ClientId = $"maintenance-relations-{Guid.NewGuid():N}",
        };
        OpenIddictMartenAuthorization[] relationAuthorizations = [];

        await using (var insertScope = provider.CreateAsyncScope())
        {
            var session = insertScope.ServiceProvider.GetRequiredService<IDocumentSession>();
            session.Insert(application, relationApplication);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);

            var authorizations = new[]
            {
                Authorization(application.Id, old, OpenIddictConstants.Statuses.Valid,
                    OpenIddictConstants.AuthorizationTypes.Permanent, "revoke-match", "openid", "profile"),
                Authorization(application.Id, old, OpenIddictConstants.Statuses.Valid,
                    OpenIddictConstants.AuthorizationTypes.Permanent, "revoke-other", "openid"),
                Authorization(application.Id, old, OpenIddictConstants.Statuses.Revoked,
                    OpenIddictConstants.AuthorizationTypes.Permanent, "prune-invalid"),
                Authorization(application.Id, old, OpenIddictConstants.Statuses.Valid,
                    OpenIddictConstants.AuthorizationTypes.AdHoc, "prune-ad-hoc"),
                Authorization(application.Id, now, OpenIddictConstants.Statuses.Revoked,
                    OpenIddictConstants.AuthorizationTypes.Permanent, "keep-new"),
                Authorization(application.Id, old, OpenIddictConstants.Statuses.Valid,
                    OpenIddictConstants.AuthorizationTypes.Permanent, "keep-valid"),
                Authorization(application.Id, old, OpenIddictConstants.Statuses.Revoked,
                    OpenIddictConstants.AuthorizationTypes.Permanent, "keep-with-token"),
            };
            session.Insert(authorizations);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);

            relationAuthorizations =
            [
                Authorization(relationApplication.Id, now, OpenIddictConstants.Statuses.Valid,
                    OpenIddictConstants.AuthorizationTypes.Permanent, "relation-one"),
                Authorization(relationApplication.Id, now, OpenIddictConstants.Statuses.Valid,
                    OpenIddictConstants.AuthorizationTypes.Permanent, "relation-two"),
            ];
            session.Insert(relationAuthorizations);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);

            var tokens = new[]
            {
                Token(application.Id, null, old, now.AddHours(1), OpenIddictConstants.Statuses.Valid, "revoke-match"),
                Token(application.Id, null, old, now.AddHours(1), OpenIddictConstants.Statuses.Valid, "revoke-other"),
                Token(application.Id, null, old, now.AddHours(1), OpenIddictConstants.Statuses.Revoked, "prune-revoked"),
                Token(application.Id, null, old, now.AddDays(-1), OpenIddictConstants.Statuses.Valid, "prune-expired"),
                Token(application.Id, authorizations[2].Id, old, now.AddHours(1), OpenIddictConstants.Statuses.Valid, "prune-invalid-auth"),
                Token(application.Id, null, now, now.AddHours(1), OpenIddictConstants.Statuses.Revoked, "keep-new"),
                Token(application.Id, null, old, now.AddHours(1), OpenIddictConstants.Statuses.Inactive, "keep-inactive"),
                Token(application.Id, authorizations[5].Id, old, now.AddHours(1), OpenIddictConstants.Statuses.Valid, "keep-valid-auth"),
                Token(application.Id, authorizations[6].Id, now, now.AddHours(1), OpenIddictConstants.Statuses.Valid, "keep-auth-attached"),
            };
            session.Insert(tokens);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);

            session.Insert(
                Token(relationApplication.Id, relationAuthorizations[0].Id, now, now.AddHours(1),
                    OpenIddictConstants.Statuses.Valid, "relation-one"),
                Token(relationApplication.Id, relationAuthorizations[1].Id, now, now.AddHours(1),
                    OpenIddictConstants.Statuses.Valid, "relation-two"));
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var maintenanceScope = provider.CreateAsyncScope())
        {
            var authorizationStore = maintenanceScope.ServiceProvider.GetRequiredService<
                IOpenIddictAuthorizationStore<OpenIddictMartenAuthorization>>();
            var tokenStore = maintenanceScope.ServiceProvider.GetRequiredService<
                IOpenIddictTokenStore<OpenIddictMartenToken>>();

            Assert.Equal(1, await authorizationStore.RevokeAsync(
                "revoke-match",
                application.Id.ToString("D"),
                OpenIddictConstants.Statuses.Valid,
                OpenIddictConstants.AuthorizationTypes.Permanent,
                TestContext.Current.CancellationToken));
            Assert.Equal(1, await tokenStore.RevokeAsync(
                "revoke-match",
                application.Id.ToString("D"),
                OpenIddictConstants.Statuses.Valid,
                OpenIddictConstants.TokenTypeHints.AccessToken,
                TestContext.Current.CancellationToken));
            Assert.Equal(1, await authorizationStore.RevokeBySubjectAsync(
                "revoke-other",
                TestContext.Current.CancellationToken));
            Assert.Equal(1, await tokenStore.RevokeBySubjectAsync(
                "revoke-other",
                TestContext.Current.CancellationToken));
            Assert.Equal(2, await authorizationStore.RevokeByApplicationIdAsync(
                relationApplication.Id.ToString("D"),
                TestContext.Current.CancellationToken));
            Assert.Equal(2, await tokenStore.RevokeByApplicationIdAsync(
                relationApplication.Id.ToString("D"),
                TestContext.Current.CancellationToken));
            Assert.Equal(1, await tokenStore.RevokeByAuthorizationIdAsync(
                relationAuthorizations[0].Id.ToString("D"),
                TestContext.Current.CancellationToken));

            Assert.Equal(5, await tokenStore.PruneAsync(threshold, TestContext.Current.CancellationToken));
            Assert.Equal(4, await authorizationStore.PruneAsync(threshold, TestContext.Current.CancellationToken));
        }

        await using var verificationScope = provider.CreateAsyncScope();
        var remainingAuthorizations = await verificationScope.ServiceProvider.GetRequiredService<
            IOpenIddictAuthorizationStore<OpenIddictMartenAuthorization>>()
            .FindByApplicationIdAsync(application.Id.ToString("D"), TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);
        var remainingTokens = await verificationScope.ServiceProvider.GetRequiredService<
            IOpenIddictTokenStore<OpenIddictMartenToken>>()
            .FindByApplicationIdAsync(application.Id.ToString("D"), TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(["keep-new", "keep-valid", "keep-with-token"],
            remainingAuthorizations.Select(value => value.Subject).Order(StringComparer.Ordinal));
        Assert.Equal(["keep-auth-attached", "keep-inactive", "keep-new", "keep-valid-auth"],
            remainingTokens.Select(value => value.Subject).Order(StringComparer.Ordinal));
    }

    [Fact(
        Skip = "Set INNOVORIUM_TEST_POSTGRES to run the disposable PostgreSQL integration test.",
        SkipUnless = nameof(HasPostgreSql))]
    public async Task MaintenanceProcessesMultipleBoundedBatchesAndReturnsExactTotals()
    {
        var now = new DateTimeOffset(2030, 7, 30, 10, 0, 0, TimeSpan.Zero);
        await using var provider = await CreateProviderAsync(new FixedTimeProvider(now));
        await using (var insertScope = provider.CreateAsyncScope())
        {
            var session = insertScope.ServiceProvider.GetRequiredService<IDocumentSession>();
            var pruneTokens = Enumerable.Range(0, MartenOpenIddictBulkOperations.BatchSize + 1)
                .Select(index => new OpenIddictMartenToken
                {
                    CreationDate = now.AddYears(-2),
                    Status = OpenIddictConstants.Statuses.Revoked,
                    Subject = $"prune-batch-{index}",
                    Type = OpenIddictConstants.TokenTypeHints.AccessToken,
                })
                .ToArray();
            var revokeTokens = Enumerable.Range(0, MartenOpenIddictBulkOperations.BatchSize + 1)
                .Select(_ => new OpenIddictMartenToken
                {
                    CreationDate = now.AddYears(-2),
                    ExpirationDate = now.AddHours(1),
                    Status = OpenIddictConstants.Statuses.Valid,
                    Subject = "revoke-batch",
                    Type = OpenIddictConstants.TokenTypeHints.AccessToken,
                })
                .ToArray();
            session.Insert(pruneTokens.Concat(revokeTokens));
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var maintenanceScope = provider.CreateAsyncScope();
        var store = maintenanceScope.ServiceProvider.GetRequiredService<
            IOpenIddictTokenStore<OpenIddictMartenToken>>();
        Assert.Equal(
            MartenOpenIddictBulkOperations.BatchSize + 1,
            await store.RevokeBySubjectAsync("revoke-batch", TestContext.Current.CancellationToken));
        Assert.Equal(
            (MartenOpenIddictBulkOperations.BatchSize + 1) * 2,
            await store.PruneAsync(now.AddYears(-1), TestContext.Current.CancellationToken));
        Assert.Equal(0, await store.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact(
        Skip = "Set INNOVORIUM_TEST_POSTGRES to run the disposable PostgreSQL integration test.",
        SkipUnless = nameof(HasPostgreSql))]
    public async Task PackageOwnedSessionsNeverFlushHostWorkAndImmediateCascadesReadFreshState()
    {
        var now = new DateTimeOffset(2030, 7, 30, 10, 0, 0, TimeSpan.Zero);
        var threshold = now.AddYears(-1);
        await using var provider = await CreateProviderAsync(new FixedTimeProvider(now));
        await using var scope = provider.CreateAsyncScope();
        var documentStore = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
        var hostSession = scope.ServiceProvider.GetRequiredService<IDocumentSession>();
        var applications = scope.ServiceProvider.GetRequiredService<
            IOpenIddictApplicationStore<OpenIddictMartenApplication>>();
        var authorizations = scope.ServiceProvider.GetRequiredService<
            IOpenIddictAuthorizationStore<OpenIddictMartenAuthorization>>();
        var scopes = scope.ServiceProvider.GetRequiredService<
            IOpenIddictScopeStore<OpenIddictMartenScope>>();
        var tokens = scope.ServiceProvider.GetRequiredService<
            IOpenIddictTokenStore<OpenIddictMartenToken>>();
        var pending = new HostPendingDocument { Name = "must-remain-pending" };
        hostSession.Store(pending);

        async Task AssertHostWorkRemainsPendingAsync()
        {
            Assert.Single(hostSession.PendingChanges.OperationsFor<HostPendingDocument>());
            await using var verification = documentStore.LightweightSession();
            Assert.Null(await verification.LoadAsync<HostPendingDocument>(
                pending.Id,
                TestContext.Current.CancellationToken));
        }

        var application = new OpenIddictMartenApplication
        {
            ClientId = $"owned-session-{Guid.NewGuid():N}",
        };
        await applications.CreateAsync(application, TestContext.Current.CancellationToken);
        await AssertHostWorkRemainsPendingAsync();

        var openIddictScope = new OpenIddictMartenScope { Name = $"scope-{Guid.NewGuid():N}" };
        await scopes.CreateAsync(openIddictScope, TestContext.Current.CancellationToken);
        await AssertHostWorkRemainsPendingAsync();

        var authorization = Authorization(
            application.Id,
            now.AddYears(-2),
            OpenIddictConstants.Statuses.Valid,
            OpenIddictConstants.AuthorizationTypes.Permanent,
            "owned-session");
        await authorizations.CreateAsync(authorization, TestContext.Current.CancellationToken);
        await AssertHostWorkRemainsPendingAsync();

        var token = Token(
            application.Id,
            authorization.Id,
            now.AddYears(-2),
            now.AddHours(1),
            OpenIddictConstants.Statuses.Valid,
            "owned-session");
        await tokens.CreateAsync(token, TestContext.Current.CancellationToken);
        await AssertHostWorkRemainsPendingAsync();

        application.DisplayName = "updated";
        await applications.UpdateAsync(application, TestContext.Current.CancellationToken);
        openIddictScope.DisplayName = "updated";
        await scopes.UpdateAsync(openIddictScope, TestContext.Current.CancellationToken);
        authorization.Subject = "owned-session-updated";
        await authorizations.UpdateAsync(authorization, TestContext.Current.CancellationToken);
        token.Subject = "owned-session-updated";
        await tokens.UpdateAsync(token, TestContext.Current.CancellationToken);
        await AssertHostWorkRemainsPendingAsync();

        Assert.Equal(1, await tokens.RevokeBySubjectAsync(
            "owned-session-updated",
            TestContext.Current.CancellationToken));
        Assert.Equal(1, await authorizations.RevokeBySubjectAsync(
            "owned-session-updated",
            TestContext.Current.CancellationToken));
        await AssertHostWorkRemainsPendingAsync();

        Assert.Equal(1, await tokens.PruneAsync(threshold, TestContext.Current.CancellationToken));
        Assert.Equal(1, await authorizations.PruneAsync(threshold, TestContext.Current.CancellationToken));
        await AssertHostWorkRemainsPendingAsync();

        await scopes.DeleteAsync(openIddictScope, TestContext.Current.CancellationToken);
        await applications.DeleteAsync(application, TestContext.Current.CancellationToken);
        await AssertHostWorkRemainsPendingAsync();

        var cascadingApplication = new OpenIddictMartenApplication
        {
            ClientId = $"owned-session-cascade-{Guid.NewGuid():N}",
        };
        var cascadingAuthorization = new OpenIddictMartenAuthorization
        {
            ApplicationId = cascadingApplication.Id,
        };
        var cascadingToken = new OpenIddictMartenToken
        {
            ApplicationId = cascadingApplication.Id,
            AuthorizationId = cascadingAuthorization.Id,
        };
        await applications.CreateAsync(cascadingApplication, TestContext.Current.CancellationToken);
        await authorizations.CreateAsync(cascadingAuthorization, TestContext.Current.CancellationToken);
        await tokens.CreateAsync(cascadingToken, TestContext.Current.CancellationToken);
        await applications.DeleteAsync(cascadingApplication, TestContext.Current.CancellationToken);

        Assert.Null(await authorizations.FindByIdAsync(
            cascadingAuthorization.Id.ToString("D"),
            TestContext.Current.CancellationToken));
        Assert.Null(await tokens.FindByIdAsync(
            cascadingToken.Id.ToString("D"),
            TestContext.Current.CancellationToken));
        await AssertHostWorkRemainsPendingAsync();
    }

    private static OpenIddictMartenAuthorization Authorization(
        Guid applicationId,
        DateTimeOffset creationDate,
        string status,
        string type,
        string subject,
        params string[] scopes)
        => new()
        {
            ApplicationId = applicationId,
            CreationDate = creationDate,
            Scopes = scopes,
            Status = status,
            Subject = subject,
            Type = type,
        };

    private static OpenIddictMartenToken Token(
        Guid applicationId,
        Guid? authorizationId,
        DateTimeOffset creationDate,
        DateTimeOffset expirationDate,
        string status,
        string subject)
        => new()
        {
            ApplicationId = applicationId,
            AuthorizationId = authorizationId,
            CreationDate = creationDate,
            ExpirationDate = expirationDate,
            Status = status,
            Subject = subject,
            Type = OpenIddictConstants.TokenTypeHints.AccessToken,
        };

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
        var verificationStore = verificationScope.ServiceProvider.GetRequiredService<
            IOpenIddictApplicationStore<OpenIddictMartenApplication>>();
        var survivor = Assert.IsType<OpenIddictMartenApplication>(await verificationStore.FindByIdAsync(
            application.Id.ToString("D"),
            TestContext.Current.CancellationToken));
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
        var verificationStore = verificationScope.ServiceProvider.GetRequiredService<
            IOpenIddictScopeStore<OpenIddictMartenScope>>();
        var survivor = Assert.IsType<OpenIddictMartenScope>(await verificationStore.FindByIdAsync(
            persistedScope.Id.ToString("D"),
            TestContext.Current.CancellationToken));
        Assert.Equal("Current", survivor.DisplayName);
        Assert.True(survivor.Version > stale.Version);
    }

    private static async Task InsertGraphAsync(
        ServiceProvider provider,
        OpenIddictMartenApplication application,
        OpenIddictMartenAuthorization authorization,
        OpenIddictMartenToken token)
    {
        await using var scope = provider.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();
        session.Insert(application);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        session.Insert(authorization);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        session.Insert(token);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<ServiceProvider> CreateProviderAsync(TimeProvider? timeProvider = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(timeProvider ?? TimeProvider.System);
        services.AddMarten(options =>
        {
            options.Connection(Environment.GetEnvironmentVariable(ConnectionVariable)!);
            options.DatabaseSchemaName = $"openiddict_{Guid.NewGuid():N}";
            options.AutoCreateSchemaObjects = AutoCreate.CreateOrUpdate;
            options.Schema.For<HostPendingDocument>().DocumentAlias("host_pending_document");
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

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class HostPendingDocument
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string? Name { get; set; }
    }
}

[CollectionDefinition("OpenIddict PostgreSQL", DisableParallelization = true)]
public sealed class OpenIddictPostgreSqlGroup;
