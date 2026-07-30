using JasperFx.Metadata;
using Microsoft.AspNetCore.Identity;

namespace Innovorium.AspNetCore.Identity.Marten;

/// <summary>
/// Provides the default, extensible string-keyed ASP.NET Core Identity role document for Marten.
/// </summary>
public class MartenIdentityRole : IdentityRole, IVersioned
{
    /// <summary>
    /// Gets or sets the Marten optimistic-concurrency version.
    /// </summary>
    public Guid Version { get; set; }
}
