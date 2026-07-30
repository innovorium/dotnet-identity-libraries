# Identity Marten source sample

This is a minimal .NET 10 host that exercises `UserManager` and `RoleManager`
through `AddMartenStores()`. It references the local provider project; it does
**not** use the nonfunctional NuGet `0.1.0-alpha.2` package.

## Run from a repository checkout

1. Provision PostgreSQL and apply the reviewed Marten schema for this exact
   source revision through your own database delivery process.
2. Keep production schema creation disabled. This sample sets `AutoCreate.None`
   and will not create, alter, or apply database objects.
3. Supply a non-production connection string outside source control and run:

```bash
ASPNETCORE_URLS='http://127.0.0.1:5050' \
ConnectionStrings__Marten='Host=localhost;Database=identity_sample;Username=postgres;Password=postgres' \
  dotnet run --project samples/Identity/Identity.Sample.csproj
```

Create a role, then create an account and assign the role:

```bash
curl -X POST http://127.0.0.1:5050/roles/member
curl -X POST http://127.0.0.1:5050/users \
  -H 'content-type: application/json' \
  -d '{"userName":"ada","email":"ada@example.test","password":"A-strong-password-1"}'
curl -X POST http://127.0.0.1:5050/users/USER_ID/roles/member
```

The account document is only an Identity record. Keep application profiles,
customer data, organization membership, and authorization policy in host-owned
models keyed by `ApplicationUser.Id`; this provider does not create or manage
them.

This source snapshot is unreleased. Read the root [README](../../README.md) and
[compatibility policy](../../docs/compatibility.md) before evaluating it.
