# OpenIddict Marten source sample

This minimal .NET 10 host registers the Marten OpenIddict stores and creates an
application through `OpenIddictApplicationManager`. It references the local
provider project rather than a NuGet package, so it always exercises the
checked-out source revision.

## What it demonstrates

`UseMarten()` registers persistence for OpenIddict core entities. It does not
configure an authorization server, endpoints, consent, issuer, signing keys, or
token validation. Those are host application decisions and must be configured
with OpenIddict's server/validation components as appropriate.

## Run from a repository checkout

1. Provision PostgreSQL and apply the reviewed Marten schema for this exact
   source revision through your own database delivery process.
2. Keep production schema creation disabled. This sample sets `AutoCreate.None`
   and will not create, alter, or apply database objects.
3. Supply a non-production connection string outside source control and run:

```bash
ASPNETCORE_URLS='http://127.0.0.1:5051' \
ConnectionStrings__Marten='Host=localhost;Database=openiddict_sample;Username=postgres;Password=postgres' \
  dotnet run --project samples/OpenIddict/OpenIddict.Sample.csproj
```

Create a client application:

```bash
curl -X POST http://127.0.0.1:5051/applications \
  -H 'content-type: application/json' \
  -d '{"clientId":"console","displayName":"Console client","permissions":["ept:token"]}'
```

Do not use this sample as an issuer configuration or a production client
registration workflow. Keep client secrets, signing credentials, issuer policy,
and operational management in the host application and secure delivery process.
Read the root [README](../../README.md) and
[compatibility policy](../../docs/compatibility.md) before evaluating it or
switching the sample to a released package reference.
