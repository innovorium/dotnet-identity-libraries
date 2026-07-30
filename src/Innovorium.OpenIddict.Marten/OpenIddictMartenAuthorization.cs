using System.Text.Json;
using JasperFx;

namespace Innovorium.OpenIddict.Marten;

/// <summary>
/// The default Marten document used to persist an OpenIddict authorization.
/// </summary>
public sealed class OpenIddictMartenAuthorization : IRevisioned
{
    /// <summary>Gets or sets the document identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the Marten optimistic-concurrency revision.</summary>
    public int Version { get; set; }

    /// <summary>Gets or sets the associated application identifier.</summary>
    public Guid? ApplicationId { get; set; }

    /// <summary>Gets or sets the UTC creation date.</summary>
    public DateTimeOffset? CreationDate { get; set; }

    /// <summary>Gets or sets additional OpenIddict properties.</summary>
    public Dictionary<string, JsonElement> Properties { get; set; } = [];

    /// <summary>Gets or sets the scopes granted by this authorization.</summary>
    public string[] Scopes { get; set; } = [];

    /// <summary>Gets or sets the OpenIddict authorization status.</summary>
    public string? Status { get; set; }

    /// <summary>Gets or sets the subject associated with this authorization.</summary>
    public string? Subject { get; set; }

    /// <summary>Gets or sets the OpenIddict authorization type.</summary>
    public string? Type { get; set; }
}
