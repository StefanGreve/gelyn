using System.Collections.Generic;

namespace Gelyn.Model;

/// <summary>
///     A content file the scan has located, before its contents are read.
/// </summary>
internal sealed record ContentItem
{
    /// <summary>
    ///     The absolute path of the file on disk.
    /// </summary>
    public required string SourcePath { get; init; }

    /// <summary>
    ///     The path of the generated file, relative to the output root, using the native directory separator.
    /// </summary>
    /// <seealso cref="Options.SiteOptions.OutputDirectory"/>
    public required string OutputPath { get; init; }

    /// <summary>
    ///     The root-relative URL the file is served at, including the path of the configured base URL.
    /// </summary>
    /// <seealso cref="Options.SiteOptions.BaseUrl"/>
    public required string Href { get; init; }

    /// <summary>
    ///     The classification the scan assigned to the file.
    /// </summary>
    public required ContentKind Kind { get; init; }

    /// <summary>
    ///     The title to fall back to when the front matter declares none: the file name, or the name of the
    ///     containing folder when the file is a section index. Meaningful for a
    ///     <see cref="ContentKind.Page"/> only.
    /// </summary>
    public required string FallbackTitle { get; init; }

    /// <summary>
    ///     Indicates whether the site navigation links the page. The landing page, root-level pages and
    ///     section indexes are linked; pages inside a section are not, and nor is a
    ///     <see cref="ContentKind.MiscFile"/>.
    /// </summary>
    public required bool InNavigation { get; init; }

    /// <summary>
    ///     The root-relative URLs of the stylesheets that apply to the file, outermost first, so that a
    ///     stylesheet deeper in the tree overrides one above it. Empty when no stylesheet applies, and
    ///     always empty for anything but a <see cref="ContentKind.Page"/>.
    /// </summary>
    public required IReadOnlyList<string> Stylesheets { get; init; }

    /// <summary>
    ///     The root-relative URLs of the scripts that apply to the file, outermost first, which is the order
    ///     they run in. Empty when no script applies, and always empty for anything but a
    ///     <see cref="ContentKind.Page"/>.
    /// </summary>
    public required IReadOnlyList<string> Scripts { get; init; }
}
