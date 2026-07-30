using Innovorium.AspNetCore.Identity.Marten;
using JasperFx;
using Marten;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);
builder.Host.ApplyJasperFxExtensions();
var connectionString = builder.Configuration.GetConnectionString("Marten")
    ?? throw new InvalidOperationException(
        "The ConnectionStrings:Marten setting is required. The host owns its PostgreSQL credentials and lifecycle.");

builder.Services.AddMarten(options =>
{
    options.Connection(connectionString);

    // Production-safe default: this application never creates or upgrades its schema.
    // Apply a reviewed Marten schema change through the host deployment process first.
    options.AutoCreateSchemaObjects = AutoCreate.None;
});

builder.Services
    .AddIdentityCore<ApplicationUser>(options => options.User.RequireUniqueEmail = true)
    .AddRoles<ApplicationRole>()
    .AddMartenStores();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    message = "Identity Marten sample. Schema deployment remains host-owned.",
}));

app.MapPost("/roles/{name}", async (
    string name,
    RoleManager<ApplicationRole> roles,
    CancellationToken cancellationToken) =>
{
    var result = await roles.CreateAsync(new ApplicationRole { Name = name });
    return result.Succeeded
        ? Results.Created($"/roles/{name}", new { name })
        : Results.ValidationProblem(ToProblemDetails(result));
});

app.MapPost("/users", async (
    CreateUserRequest request,
    UserManager<ApplicationUser> users,
    CancellationToken cancellationToken) =>
{
    var user = new ApplicationUser
    {
        UserName = request.UserName,
        Email = request.Email,
    };
    var result = await users.CreateAsync(user, request.Password);

    return result.Succeeded
        ? Results.Created($"/users/{user.Id}", new { user.Id, user.UserName, user.Email })
        : Results.ValidationProblem(ToProblemDetails(result));
});

app.MapPost("/users/{id}/roles/{role}", async (
    string id,
    string role,
    UserManager<ApplicationUser> users,
    CancellationToken cancellationToken) =>
{
    var user = await users.FindByIdAsync(id);
    if (user is null)
    {
        return Results.NotFound();
    }

    var result = await users.AddToRoleAsync(user, role);
    return result.Succeeded
        ? Results.NoContent()
        : Results.ValidationProblem(ToProblemDetails(result));
});

return await app.RunJasperFxCommands(args);

static Dictionary<string, string[]> ToProblemDetails(IdentityResult result) =>
    result.Errors
        .GroupBy(error => error.Code)
        .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray());

internal sealed class ApplicationUser : MartenIdentityUser;

internal sealed class ApplicationRole : MartenIdentityRole;

internal sealed record CreateUserRequest(string UserName, string Email, string Password);
