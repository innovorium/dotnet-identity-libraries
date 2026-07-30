using System.Text.Json;
using JasperFx;

namespace Innovorium.OpenIddict.Marten;

/// <summary>
/// The default Marten document used to persist an OpenIddict scope.
/// </summary>
public sealed class OpenIddictMartenScope : IRevisioned
{
    /// <summary>Gets or sets the document identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the Marten optimistic-concurrency revision.</summary>
    public int Version { get; set; }

    /// <summary>Gets or sets the description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets localized descriptions keyed by culture name.</summary>
    public Dictionary<string, string> Descriptions { get; set; } = [];

    /// <summary>Gets or sets the display name.</summary>
    public string? DisplayName { get; set; }

    /// <summary>Gets or sets localized display names keyed by culture name.</summary>
    public Dictionary<string, string> DisplayNames { get; set; } = [];

    /// <summary>Gets or sets the unique scope name.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets additional OpenIddict properties.</summary>
    public Dictionary<string, JsonElement> Properties { get; set; } = [];

    /// <summary>Gets or sets the resources associated with this scope.</summary>
    public string[] Resources { get; set; } = [];
}
