using System;

namespace Gelyn.Model;

/// <summary>
///     A discovered content file, rendered and ready to be laid out.
/// </summary>
public sealed record ContentPage
{
    /// <summary>
    ///     The path of the generated file, relative to the output root, using the native directory separator.
    /// </summary>
    /// <seealso cref="Options.SiteOptions.OutputDirectory"/>
    public required string OutputPath { get; init; }

    /// <summary>
    ///     The root-relative URL the page is linked by, including the path of the configured base URL.
    /// </summary>
    /// <seealso cref="Options.SiteOptions.BaseUrl"/>
    public required string Href { get; init; }

    /// <summary>
    ///     The page title, falling back to the file or folder name when the front matter omits it.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    ///     The description declared in the front matter, or <see langword="null"/> to fall back to the
    ///     description configured for the site.
    /// </summary>
    /// <seealso cref="Options.SiteOptions.Description"/>
    public required string? Description { get; init; }

    /// <summary>
    ///     The publication date declared in the front matter, or <see langword="null"/> when it declares none.
    /// </summary>
    public required DateOnly? Date { get; init; }

    /// <summary>
    ///     The rendered HTML body, excluding the document shell.
    /// </summary>
    public required string Html { get; init; }

    /// <summary>
    ///     Indicates whether the site navigation links the page. The landing page, root-level pages
    ///     and section indexes are linked; pages inside a section are not.
    /// </summary>
    public required bool InNavigation { get; init; }
}
