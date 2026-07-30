using JasperFx.Metadata;
using Microsoft.AspNetCore.Identity;

namespace Innovorium.AspNetCore.Identity.Marten;

/// <summary>
/// Provides the default, extensible string-keyed ASP.NET Core Identity user document for Marten.
/// </summary>
public class MartenIdentityUser : IdentityUser, IVersioned
{
    /// <summary>
    /// Gets or sets the Marten optimistic-concurrency version.
    /// </summary>
    public Guid Version { get; set; }
}
