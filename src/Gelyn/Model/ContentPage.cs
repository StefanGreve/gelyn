namespace Gelyn.Model;

/// <summary>
///     A discovered content file, rendered and ready to be laid out.
/// </summary>
public sealed record ContentPage
{
    /// <summary>
    ///     The path of the generated file, relative to the output root, using the native directory separator.
    /// </summary>
    public required string OutputPath { get; init; }

    /// <summary>
    ///     The root-relative URL the page is linked by.
    /// </summary>
    public required string Href { get; init; }

    /// <summary>
    ///     The page title, falling back to the file or folder name when the front matter omits it.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    ///     The rendered HTML body, excluding the document shell.
    /// </summary>
    public required string Html { get; init; }
}
