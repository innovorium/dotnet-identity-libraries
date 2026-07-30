using System.Text.Json;
using JasperFx;

namespace Innovorium.OpenIddict.Marten;

/// <summary>
/// The default Marten document used to persist an OpenIddict token.
/// </summary>
public sealed class OpenIddictMartenToken : IRevisioned
{
    /// <summary>Gets or sets the document identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the Marten optimistic-concurrency revision.</summary>
    public int Version { get; set; }

    /// <summary>Gets or sets the associated application identifier.</summary>
    public Guid? ApplicationId { get; set; }

    /// <summary>Gets or sets the associated authorization identifier.</summary>
    public Guid? AuthorizationId { get; set; }

    /// <summary>Gets or sets the UTC creation date.</summary>
    public DateTimeOffset? CreationDate { get; set; }

    /// <summary>Gets or sets the UTC expiration date.</summary>
    public DateTimeOffset? ExpirationDate { get; set; }

    /// <summary>Gets or sets the protected token payload.</summary>
    public string? Payload { get; set; }

    /// <summary>Gets or sets additional OpenIddict properties.</summary>
    public Dictionary<string, JsonElement> Properties { get; set; } = [];

    /// <summary>Gets or sets the UTC redemption date.</summary>
    public DateTimeOffset? RedemptionDate { get; set; }

    /// <summary>Gets or sets the hashed reference identifier.</summary>
    public string? ReferenceId { get; set; }

    /// <summary>Gets or sets the OpenIddict token status.</summary>
    public string? Status { get; set; }

    /// <summary>Gets or sets the subject associated with this token.</summary>
    public string? Subject { get; set; }

    /// <summary>Gets or sets the OpenIddict token type.</summary>
    public string? Type { get; set; }
}
