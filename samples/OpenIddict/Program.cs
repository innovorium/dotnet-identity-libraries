using Innovorium.OpenIddict.Marten;
using JasperFx;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Core;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Marten")
    ?? throw new InvalidOperationException(
        "The ConnectionStrings:Marten setting is required. The host owns its PostgreSQL credentials and lifecycle.");

builder.Services.AddMarten(options =>
{
    options.Connection(connectionString);

    // This sample never creates or upgrades its schema. Apply reviewed changes outside the app.
    options.AutoCreateSchemaObjects = AutoCreate.None;
});

builder.Services.AddOpenIddict()
    .AddCore(options => options.UseMarten());

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    message = "OpenIddict Marten sample. It registers stores only, not an authorization server.",
}));

app.MapPost("/applications", async (
    CreateApplicationRequest request,
    OpenIddictApplicationManager<OpenIddictMartenApplication> applications,
    CancellationToken cancellationToken) =>
{
    var descriptor = new OpenIddictApplicationDescriptor
    {
        ClientId = request.ClientId,
        DisplayName = request.DisplayName,
    };

    foreach (var permission in request.Permissions ?? [])
    {
        descriptor.Permissions.Add(permission);
    }

    await applications.CreateAsync(descriptor, cancellationToken);
    var application = await applications.FindByClientIdAsync(request.ClientId, cancellationToken);

    return application is null
        ? Results.Problem("The application was not found after creation.")
        : Results.Created($"/applications/{request.ClientId}", new { request.ClientId, request.DisplayName });
});

app.Run();

internal sealed record CreateApplicationRequest(
    string ClientId,
    string? DisplayName,
    string[]? Permissions);
