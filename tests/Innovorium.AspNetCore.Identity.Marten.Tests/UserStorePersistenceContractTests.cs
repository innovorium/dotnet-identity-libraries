using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Marten;
using Marten.Exceptions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace Innovorium.AspNetCore.Identity.Marten.Tests;

public sealed class UserStorePersistenceContractTests
{
    [Fact]
    public async Task CreateUsesInsertAndCommitsTheDedicatedSession()
    {
        using var fixture = new StoreFixture();
        using var store = fixture.CreateStore();

        var result = await store.CreateAsync(
            new ApplicationUser { Id = "new-user" },
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Contains("Insert", fixture.Session.Calls);
        Assert.Contains("SaveChangesAsync", fixture.Session.Calls);
        Assert.DoesNotContain("Store", fixture.Session.Calls);
    }

    [Fact]
    public async Task UpdateStagesAnOptimisticUpdateRotatesTheIdentityStampAndCommits()
    {
        using var fixture = new StoreFixture();
        using var store = fixture.CreateStore();
        var user = new ApplicationUser { ConcurrencyStamp = "original-stamp" };

        var result = await store.UpdateAsync(user, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotEqual("original-stamp", user.ConcurrencyStamp);
        Assert.Contains("Update", fixture.Session.Calls);
        Assert.Contains("SaveChangesAsync", fixture.Session.Calls);
    }

    [Fact]
    public async Task DeleteUsesOneVersionGuardedDatabaseCommand()
    {
        using var fixture = new StoreFixture();
        using var store = fixture.CreateStore();
        var user = new ApplicationUser { Id = "existing-user", Version = Guid.NewGuid() };

        var result = await store.DeleteAsync(user, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Contains("QueryAsync", fixture.Session.Calls);
        Assert.DoesNotContain("Delete", fixture.Session.Calls);
        Assert.DoesNotContain("SaveChangesAsync", fixture.Session.Calls);

        var invocation = Assert.Single(fixture.Session.Invocations, call => call.Name == "QueryAsync");
        var sql = Assert.IsType<string>(invocation.Arguments[0]);
        var parameters = Assert.IsType<object[]>(invocation.Arguments[2]);
        Assert.Contains("where id = ? and mt_version = ?", sql, StringComparison.Ordinal);
        Assert.StartsWith("with deleted_user as (delete from ", sql, StringComparison.Ordinal);
        Assert.Contains("mt_doc_identity_user_claim", sql, StringComparison.Ordinal);
        Assert.Contains("mt_doc_identity_user_login", sql, StringComparison.Ordinal);
        Assert.Contains("mt_doc_identity_user_token", sql, StringComparison.Ordinal);
        Assert.Contains("mt_doc_identity_user_passkey", sql, StringComparison.Ordinal);
        Assert.Contains("where user_id in (select id from deleted_user)", sql, StringComparison.Ordinal);
        Assert.EndsWith("select data from deleted_user", sql, StringComparison.Ordinal);
        Assert.Equal([user.Id, user.Version], parameters);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StaleMutationSignalReturnsTheStandardIdentityConcurrencyFailure(bool update)
    {
        using var fixture = new StoreFixture();
        fixture.Session.ResultFactory = method =>
            update && method.Name == nameof(IDocumentSession.SaveChangesAsync)
                ? throw new ConcurrentUpdateException(new InvalidOperationException("stale document"))
                : !update && method.Name == nameof(IQuerySession.QueryAsync)
                    ? Task.FromResult<IReadOnlyList<ApplicationUser>>([])
                    : RecordingProxy.DefaultResult(method);
        using var store = fixture.CreateStore();
        var user = new ApplicationUser { Id = "stale-user", Version = Guid.NewGuid() };

        var result = update
            ? await store.UpdateAsync(user, TestContext.Current.CancellationToken)
            : await store.DeleteAsync(user, TestContext.Current.CancellationToken);

        var error = Assert.Single(result.Errors);
        Assert.False(result.Succeeded);
        Assert.Equal("ConcurrencyFailure", error.Code);
        Assert.Equal(update ? 2 : 1, DocumentStoreSessionOpenCount(fixture));
    }

    [Theory]
    [InlineData("uidx_identity_user_normalized_username", "DuplicateUserName")]
    [InlineData("uidx_identity_user_normalized_email", "DuplicateEmail")]
    public async Task KnownUniqueConstraintsReturnStandardIdentityFailures(
        string constraintName,
        string expectedErrorCode)
    {
        using var fixture = new StoreFixture();
        fixture.Session.ResultFactory = method =>
            method.Name == nameof(IDocumentSession.SaveChangesAsync)
                ? throw new AggregateException(CreateUniqueViolation(constraintName))
                : RecordingProxy.DefaultResult(method);
        using var store = fixture.CreateStore();

        var result = await store.CreateAsync(
            new ApplicationUser { UserName = "ada", Email = "ada@example.test" },
            TestContext.Current.CancellationToken);

        var error = Assert.Single(result.Errors);
        Assert.False(result.Succeeded);
        Assert.Equal(expectedErrorCode, error.Code);
        Assert.Equal(2, DocumentStoreSessionOpenCount(fixture));
    }

    [Fact]
    public async Task PasskeyOwnedByAnotherUserReturnsStableFailureWithoutMutationOrBufferLeak()
    {
        using var fixture = new StoreFixture();
        var credentialId = new byte[] { 1, 2, 3, 4 };
        var original = new MartenIdentityUserPasskey<ApplicationUser>
        {
            Id = MartenIdentityDocumentId.UserPasskey(credentialId),
            UserId = "owner",
        };
        original.Update(CreatePasskey(credentialId, "Owner key", [5, 6, 7]));
        fixture.Session.ResultFactory = method =>
            method.Name == nameof(IQuerySession.LoadAsync)
                ? Task.FromResult<MartenIdentityUserPasskey<ApplicationUser>?>(original)
                : RecordingProxy.DefaultResult(method);
        using var store = fixture.CreateStore();
        var intruder = new ApplicationUser { Id = "second-user" };

        await store.AddOrUpdatePasskeyAsync(
            intruder,
            CreatePasskey(credentialId, "Replacement", [9, 9, 9]),
            TestContext.Current.CancellationToken);
        var rejected = await store.UpdateAsync(intruder, TestContext.Current.CancellationToken);

        Assert.False(rejected.Succeeded);
        Assert.Equal("DuplicatePasskey", Assert.Single(rejected.Errors).Code);
        Assert.Equal("owner", original.UserId);
        Assert.Equal("Owner key", original.Name);
        Assert.Equal([5, 6, 7], original.PublicKey);
        Assert.DoesNotContain("Store", fixture.Session.Calls);
        Assert.DoesNotContain("Update", fixture.Session.Calls);
        Assert.DoesNotContain("SaveChangesAsync", fixture.Session.Calls);

        fixture.Session.Calls.Clear();
        intruder.PhoneNumber = "+15550000003";
        var laterUpdate = await store.UpdateAsync(intruder, TestContext.Current.CancellationToken);
        Assert.True(laterUpdate.Succeeded);
        Assert.Contains("Update", fixture.Session.Calls);
        Assert.Contains("SaveChangesAsync", fixture.Session.Calls);
        Assert.DoesNotContain("Store", fixture.Session.Calls);
    }

    [Fact]
    public async Task ConcurrentPasskeyInsertReturnsStableFailureAndResetsTheSession()
    {
        using var fixture = new StoreFixture();
        fixture.Session.ResultFactory = method =>
            method.Name == nameof(IDocumentSession.SaveChangesAsync)
                ? throw new AggregateException(
                    CreateUniqueViolation(MartenIdentitySchema.UserPasskeyPrimaryKey))
                : RecordingProxy.DefaultResult(method);
        using var store = fixture.CreateStore();
        var user = new ApplicationUser { Id = "race-user" };

        await store.AddOrUpdatePasskeyAsync(
            user,
            CreatePasskey([1, 3, 3, 7], "Racing key", [2, 4, 6]),
            TestContext.Current.CancellationToken);
        var result = await store.UpdateAsync(user, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("DuplicatePasskey", Assert.Single(result.Errors).Code);
        Assert.Contains("Insert", fixture.Session.Calls);
        Assert.Contains("SaveChangesAsync", fixture.Session.Calls);
        Assert.Equal(2, DocumentStoreSessionOpenCount(fixture));
    }

    private static int DocumentStoreSessionOpenCount(StoreFixture fixture) =>
        fixture.DocumentStore.Calls.Count(call => call == nameof(IDocumentStore.LightweightSession));

    private static PostgresException CreateUniqueViolation(string constraintName) =>
        new(
            messageText: "duplicate key",
            severity: "ERROR",
            invariantSeverity: "ERROR",
            sqlState: "23505",
            detail: string.Empty,
            hint: string.Empty,
            position: 0,
            internalPosition: 0,
            internalQuery: string.Empty,
            where: string.Empty,
            schemaName: "public",
            tableName: "mt_doc_identity_user",
            columnName: string.Empty,
            dataTypeName: string.Empty,
            constraintName: constraintName,
            file: string.Empty,
            line: string.Empty,
            routine: string.Empty);

    private static UserPasskeyInfo CreatePasskey(byte[] credentialId, string name, byte[] publicKey) =>
        new(
            credentialId,
            publicKey,
            DateTimeOffset.UtcNow,
            3,
            ["internal"],
            isUserVerified: true,
            isBackupEligible: false,
            isBackedUp: false,
            [8],
            [9])
        {
            Name = name,
        };

    private sealed class StoreFixture : IDisposable
    {
        private readonly IDocumentStore _documentStore;
        private readonly IDocumentStore _mappingStore;

        public StoreFixture()
        {
            _mappingStore = global::Marten.DocumentStore.For(options =>
            {
                options.Connection(
                    "Host=127.0.0.1;Port=1;Database=identity_tests;Username=identity_tests;Password=identity_tests");
                MartenIdentitySchema.ConfigureUser<ApplicationUser>(options, requireUniqueEmail: false);
            });
            var session = DispatchProxy.Create<IDocumentSession, RecordingProxy>();
            Session = (RecordingProxy)(object)session;

            _documentStore = DispatchProxy.Create<IDocumentStore, RecordingProxy>();
            DocumentStore = (RecordingProxy)(object)_documentStore;
            DocumentStore.ResultFactory = method =>
                method.Name == nameof(IDocumentStore.LightweightSession)
                    ? session
                    : method.Name == "get_Options"
                        ? _mappingStore.Options
                        : RecordingProxy.DefaultResult(method);
        }

        public RecordingProxy DocumentStore { get; }

        public RecordingProxy Session { get; }

        public MartenUserOnlyStore<ApplicationUser> CreateStore() =>
            new(_documentStore, Options.Create(new IdentityOptions()), new IdentityErrorDescriber());

        public void Dispose() => _mappingStore.Dispose();
    }

    private sealed class ApplicationUser : MartenIdentityUser;
}

[SuppressMessage("Performance", "CA1852:Seal internal types", Justification = "DispatchProxy generates a runtime subclass.")]
internal class RecordingProxy : DispatchProxy
{
    public List<string> Calls { get; } = [];

    public List<(string Name, object?[] Arguments)> Invocations { get; } = [];

    public Func<MethodInfo, object?> ResultFactory { get; set; } = DefaultResult;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        Calls.Add(targetMethod.Name);
        Invocations.Add((targetMethod.Name, args ?? []));
        return ResultFactory(targetMethod);
    }

    internal static object? DefaultResult(MethodInfo method)
    {
        if (method.ReturnType == typeof(void))
        {
            return null;
        }

        if (method.ReturnType == typeof(Task))
        {
            return Task.CompletedTask;
        }

        if (method.ReturnType == typeof(ValueTask))
        {
            return ValueTask.CompletedTask;
        }

        if (method.ReturnType.IsGenericType &&
            method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var resultType = method.ReturnType.GenericTypeArguments[0];
            if (method.Name == nameof(IQuerySession.QueryAsync))
            {
                var elementType = resultType.GenericTypeArguments[0];
                var values = Array.CreateInstance(elementType, 1);
                values.SetValue(Activator.CreateInstance(elementType), 0);
                return typeof(Task)
                    .GetMethod(nameof(Task.FromResult))!
                    .MakeGenericMethod(resultType)
                    .Invoke(null, [values]);
            }

            return typeof(Task)
                .GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(resultType)
                .Invoke(null, [resultType.IsValueType ? Activator.CreateInstance(resultType) : null]);
        }

        return method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
    }
}
