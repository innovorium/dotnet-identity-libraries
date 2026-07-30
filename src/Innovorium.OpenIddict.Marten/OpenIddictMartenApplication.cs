using System.Text.Json;
using JasperFx;

namespace Innovorium.OpenIddict.Marten;

/// <summary>
/// The default Marten document used to persist an OpenIddict application.
/// </summary>
public sealed class OpenIddictMartenApplication : IRevisioned
{
    /// <summary>Gets or sets the document identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the Marten optimistic-concurrency revision.</summary>
    public int Version { get; set; }

    /// <summary>Gets or sets the OpenIddict application type.</summary>
    public string? ApplicationType { get; set; }

    /// <summary>Gets or sets the OAuth client identifier.</summary>
    public string? ClientId { get; set; }

    /// <summary>Gets or sets the hashed client secret.</summary>
    public string? ClientSecret { get; set; }

    /// <summary>Gets or sets the OpenIddict client type.</summary>
    public string? ClientType { get; set; }

    /// <summary>Gets or sets the OpenIddict consent type.</summary>
    public string? ConsentType { get; set; }

    /// <summary>Gets or sets the display name.</summary>
    public string? DisplayName { get; set; }

    /// <summary>Gets or sets localized display names keyed by culture name.</summary>
    public Dictionary<string, string> DisplayNames { get; set; } = [];

    /// <summary>Gets or sets the serialized JSON Web Key Set.</summary>
    public string? JsonWebKeySet { get; set; }

    /// <summary>Gets or sets the OpenIddict permissions.</summary>
    public string[] Permissions { get; set; } = [];

    /// <summary>Gets or sets the post-logout redirect URIs.</summary>
    public string[] PostLogoutRedirectUris { get; set; } = [];

    /// <summary>Gets or sets additional OpenIddict properties.</summary>
    public Dictionary<string, JsonElement> Properties { get; set; } = [];

    /// <summary>Gets or sets the redirect URIs.</summary>
    public string[] RedirectUris { get; set; } = [];

    /// <summary>Gets or sets the OpenIddict requirements.</summary>
    public string[] Requirements { get; set; } = [];

    /// <summary>Gets or sets the OpenIddict settings.</summary>
    public Dictionary<string, string> Settings { get; set; } = [];
}
