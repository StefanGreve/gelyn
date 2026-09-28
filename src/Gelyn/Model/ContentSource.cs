namespace Gelyn.Model;

/// <summary>
///     A content file the walk has located, before its Markdown is read and rendered.
/// </summary>
/// <remarks>
///     Projected onto a <see cref="ContentPage"/> once the source has been read, which is where the
///     corresponding members are documented in their rendered form.
/// </remarks>
internal sealed record ContentSource
{
    /// <summary>
    ///     The absolute path of the Markdown file to read.
    /// </summary>
    public required string SourcePath { get; init; }

    /// <summary>
    ///     The path of the generated file relative to the output root, as one segment per directory
    ///     followed by the file name.
    /// </summary>
    public required string[] Segments { get; init; }

    /// <summary>
    ///     The title to fall back to when the front matter declares none: the file name, or the name of the
    ///     containing folder when the source is a section index.
    /// </summary>
    public required string FallbackTitle { get; init; }

    /// <summary>
    ///     Indicates whether the site navigation links the page. The landing page, root-level pages and
    ///     section indexes are linked; pages inside a section are not.
    /// </summary>
    public required bool InNavigation { get; init; }
}
