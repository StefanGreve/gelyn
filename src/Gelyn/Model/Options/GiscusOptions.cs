using System.ComponentModel.DataAnnotations;

namespace Gelyn.Model.Options;

/// <summary>
///     The GitHub Discussions backend giscus renders comments from.
/// </summary>
/// <remarks>
///     A member the configuration omits stays <see langword="null"/>, which is not a usable value.
/// </remarks>
/// <seealso cref="SiteOptions.Giscus"/>
internal sealed class GiscusOptions
{
    /// <summary>
    ///     The repository, as <c>owner/name</c>.
    /// </summary>
    [Required]
    [RegularExpression(@"^[^/\s]+/[^/\s]+$")]
    public string? Repo { get; set; }

    /// <summary>
    ///     The repository id the giscus configuration page shows.
    /// </summary>
    [Required]
    public string? RepoId { get; set; }

    /// <summary>
    ///     The discussion category giscus creates threads in.
    /// </summary>
    [Required]
    public string? Category { get; set; }

    /// <summary>
    ///     The category id the giscus configuration page shows.
    /// </summary>
    [Required]
    public string? CategoryId { get; set; }
}
