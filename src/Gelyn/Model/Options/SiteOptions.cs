using System;

namespace Gelyn.Model.Options;

/// <summary>
///     Locations and metadata the generator works with, bound from the root of the configuration.
/// </summary>
/// <seealso cref="Validators.SiteOptionsValidator"/>
public sealed class SiteOptions
{
    /// <summary>
    ///     The name of the site.
    /// </summary>
    public string Title { get; set; } = "Gelyn";

    /// <summary>
    ///     The language of the generated pages, written to the <c>lang</c> attribute of every document.
    /// </summary>
    public string Language { get; set; } = "en";

    /// <summary>
    ///     The name written to the author meta tag, or <see langword="null"/> to write no author at all.
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    ///     The description written to the description meta tag by any page whose front matter declares none,
    ///     or <see langword="null"/> to leave such pages without a description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     The absolute URL the finished site is served from, such as <c>https://example.com/blog</c>, or
    ///     <see langword="null"/> when it is served from the root of a domain.
    /// </summary>
    public Uri? BaseUrl { get; set; }

    /// <summary>
    ///     The directory holding the Markdown sources. Relative values are resolved against the working directory.
    /// </summary>
    public string ContentDirectory { get; set; } = "content";

    /// <summary>
    ///     The directory the generated site is written to. Relative values are resolved against the working directory.
    /// </summary>
    public string OutputDirectory { get; set; } = "_site";
}
