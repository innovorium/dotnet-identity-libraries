using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using JasperFx;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using Xunit;

namespace Innovorium.OpenIddict.Marten.Tests;

public sealed class DocumentContractTests
{
    [Fact]
    public async Task ApplicationStoreRoundTripsOpenIddictValues()
    {
        var session = RecordingDocumentSession.Create(out _);
        var store = new MartenOpenIddictApplicationStore(session);
        Assert.IsAssignableFrom<IOpenIddictApplicationStore<OpenIddictMartenApplication>>(store);
        var application = await store.InstantiateAsync(TestContext.Current.CancellationToken);
        var names = ImmutableDictionary<CultureInfo, string>.Empty
            .Add(CultureInfo.GetCultureInfo("en-US"), "Customer portal");
        var permissions = ImmutableArray.Create("ept:authorization", "gt:authorization_code");
        var settings = ImmutableDictionary<string, string>.Empty.Add("setting", "value");
        var set = new JsonWebKeySet("{\"keys\":[]}");

        await store.SetClientIdAsync(application, "customer-portal", TestContext.Current.CancellationToken);
        await store.SetDisplayNamesAsync(application, names, TestContext.Current.CancellationToken);
        await store.SetPermissionsAsync(application, permissions, TestContext.Current.CancellationToken);
        await store.SetSettingsAsync(application, settings, TestContext.Current.CancellationToken);
        await store.SetJsonWebKeySetAsync(application, set, TestContext.Current.CancellationToken);

        Assert.Equal("customer-portal", await store.GetClientIdAsync(application, TestContext.Current.CancellationToken));
        Assert.Equal(names, await store.GetDisplayNamesAsync(application, TestContext.Current.CancellationToken));
        Assert.Equal(permissions, await store.GetPermissionsAsync(application, TestContext.Current.CancellationToken));
        Assert.Equal(settings, await store.GetSettingsAsync(application, TestContext.Current.CancellationToken));
        Assert.NotNull(await store.GetJsonWebKeySetAsync(application, TestContext.Current.CancellationToken));
        Assert.True(Guid.TryParse(await store.GetIdAsync(application, TestContext.Current.CancellationToken), out _));
        Assert.IsAssignableFrom<IRevisioned>(application);
    }

    [Fact]
    public async Task ScopeStoreRoundTripsOpenIddictValues()
    {
        var session = RecordingDocumentSession.Create(out _);
        var store = new MartenOpenIddictScopeStore(session);
        Assert.IsAssignableFrom<IOpenIddictScopeStore<OpenIddictMartenScope>>(store);
        var scope = await store.InstantiateAsync(TestContext.Current.CancellationToken);
        var descriptions = ImmutableDictionary<CultureInfo, string>.Empty
            .Add(CultureInfo.GetCultureInfo("fr-FR"), "Profil");
        var resources = ImmutableArray.Create("customer-api", "reporting-api");

        await store.SetNameAsync(scope, "profile", TestContext.Current.CancellationToken);
        await store.SetDescriptionsAsync(scope, descriptions, TestContext.Current.CancellationToken);
        await store.SetResourcesAsync(scope, resources, TestContext.Current.CancellationToken);

        Assert.Equal("profile", await store.GetNameAsync(scope, TestContext.Current.CancellationToken));
        Assert.Equal(descriptions, await store.GetDescriptionsAsync(scope, TestContext.Current.CancellationToken));
        Assert.Equal(resources, await store.GetResourcesAsync(scope, TestContext.Current.CancellationToken));
        Assert.True(Guid.TryParse(await store.GetIdAsync(scope, TestContext.Current.CancellationToken), out _));
        Assert.IsAssignableFrom<IRevisioned>(scope);
    }

    [Fact]
    public async Task AuthorizationStoreRoundTripsOpenIddictValues()
    {
        var session = RecordingDocumentSession.Create(out _);
        var store = new MartenOpenIddictAuthorizationStore(session);
        Assert.IsAssignableFrom<IOpenIddictAuthorizationStore<OpenIddictMartenAuthorization>>(store);
        var authorization = await store.InstantiateAsync(TestContext.Current.CancellationToken);
        var applicationId = Guid.NewGuid();
        var creationDate = DateTimeOffset.Parse(
            "2026-07-30T10:00:00Z",
            CultureInfo.InvariantCulture);
        var scopes = ImmutableArray.Create("openid", "profile");

        await store.SetApplicationIdAsync(
            authorization,
            applicationId.ToString("D"),
            TestContext.Current.CancellationToken);
        await store.SetCreationDateAsync(authorization, creationDate, TestContext.Current.CancellationToken);
        await store.SetScopesAsync(authorization, scopes, TestContext.Current.CancellationToken);
        await store.SetStatusAsync(
            authorization,
            OpenIddictConstants.Statuses.Valid,
            TestContext.Current.CancellationToken);
        await store.SetSubjectAsync(authorization, "customer-42", TestContext.Current.CancellationToken);
        await store.SetTypeAsync(
            authorization,
            OpenIddictConstants.AuthorizationTypes.Permanent,
            TestContext.Current.CancellationToken);

        Assert.Equal(
            applicationId.ToString("D"),
            await store.GetApplicationIdAsync(authorization, TestContext.Current.CancellationToken));
        Assert.Equal(creationDate, await store.GetCreationDateAsync(
            authorization,
            TestContext.Current.CancellationToken));
        Assert.Equal(scopes, await store.GetScopesAsync(authorization, TestContext.Current.CancellationToken));
        Assert.Equal(OpenIddictConstants.Statuses.Valid, await store.GetStatusAsync(
            authorization,
            TestContext.Current.CancellationToken));
        Assert.Equal("customer-42", await store.GetSubjectAsync(
            authorization,
            TestContext.Current.CancellationToken));
        Assert.Equal(OpenIddictConstants.AuthorizationTypes.Permanent, await store.GetTypeAsync(
            authorization,
            TestContext.Current.CancellationToken));
        Assert.IsAssignableFrom<IRevisioned>(authorization);
    }

    [Fact]
    public async Task TokenStoreRoundTripsOpenIddictValues()
    {
        var session = RecordingDocumentSession.Create(out _);
        var store = new MartenOpenIddictTokenStore(session, TimeProvider.System);
        Assert.IsAssignableFrom<IOpenIddictTokenStore<OpenIddictMartenToken>>(store);
        var token = await store.InstantiateAsync(TestContext.Current.CancellationToken);
        var applicationId = Guid.NewGuid();
        var authorizationId = Guid.NewGuid();
        var creationDate = DateTimeOffset.Parse("2026-07-30T10:00:00Z", CultureInfo.InvariantCulture);
        var expirationDate = creationDate.AddHours(1);
        var redemptionDate = creationDate.AddMinutes(5);

        await store.SetApplicationIdAsync(token, applicationId.ToString("D"), TestContext.Current.CancellationToken);
        await store.SetAuthorizationIdAsync(token, authorizationId.ToString("D"), TestContext.Current.CancellationToken);
        await store.SetCreationDateAsync(token, creationDate, TestContext.Current.CancellationToken);
        await store.SetExpirationDateAsync(token, expirationDate, TestContext.Current.CancellationToken);
        await store.SetPayloadAsync(token, "protected-payload", TestContext.Current.CancellationToken);
        await store.SetRedemptionDateAsync(token, redemptionDate, TestContext.Current.CancellationToken);
        await store.SetReferenceIdAsync(token, "hashed-reference", TestContext.Current.CancellationToken);
        await store.SetStatusAsync(token, OpenIddictConstants.Statuses.Valid, TestContext.Current.CancellationToken);
        await store.SetSubjectAsync(token, "customer-42", TestContext.Current.CancellationToken);
        await store.SetTypeAsync(token, OpenIddictConstants.TokenTypeHints.AccessToken, TestContext.Current.CancellationToken);

        Assert.Equal(applicationId.ToString("D"), await store.GetApplicationIdAsync(
            token,
            TestContext.Current.CancellationToken));
        Assert.Equal(authorizationId.ToString("D"), await store.GetAuthorizationIdAsync(
            token,
            TestContext.Current.CancellationToken));
        Assert.Equal(creationDate, await store.GetCreationDateAsync(token, TestContext.Current.CancellationToken));
        Assert.Equal(expirationDate, await store.GetExpirationDateAsync(token, TestContext.Current.CancellationToken));
        Assert.Equal("protected-payload", await store.GetPayloadAsync(token, TestContext.Current.CancellationToken));
        Assert.Equal(redemptionDate, await store.GetRedemptionDateAsync(token, TestContext.Current.CancellationToken));
        Assert.Equal("hashed-reference", await store.GetReferenceIdAsync(token, TestContext.Current.CancellationToken));
        Assert.IsAssignableFrom<IRevisioned>(token);
    }

    [Fact]
    public async Task PropertyValuesAreDetachedFromTheSourceJsonDocument()
    {
        var session = RecordingDocumentSession.Create(out _);
        var store = new MartenOpenIddictApplicationStore(session);
        var application = new OpenIddictMartenApplication();
        JsonElement value;

        using (var document = JsonDocument.Parse("{\"customer\":{\"tier\":\"gold\"}}"))
        {
            value = document.RootElement.GetProperty("customer");
            await store.SetPropertiesAsync(
                application,
                ImmutableDictionary<string, JsonElement>.Empty.Add("profile", value),
                TestContext.Current.CancellationToken);
        }

        var properties = await store.GetPropertiesAsync(application, TestContext.Current.CancellationToken);
        Assert.Equal("gold", properties["profile"].GetProperty("tier").GetString());
    }

    [Fact]
    public void ScopeNameSetRejectsDefaultEmptyAndInvalidValues()
    {
        var session = RecordingDocumentSession.Create(out _);
        var store = new MartenOpenIddictScopeStore(session);

        Assert.Throws<ArgumentException>(() =>
            store.FindByNamesAsync(default, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() =>
            store.FindByNamesAsync([], TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() =>
            store.FindByNamesAsync(["profile", ""], TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() =>
            store.FindByNamesAsync(["profile", null!], TestContext.Current.CancellationToken));
    }
}
