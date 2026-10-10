namespace Gelyn.Model;

/// <summary>
///     How the build treats a file the scan located.
/// </summary>
public enum ContentKind
{
    /// <summary>
    ///     Markdown, rendered into an HTML document.
    /// </summary>
    Page,

    /// <summary>
    ///     Anything else, copied to the output directory byte for byte.
    /// </summary>
    MiscFile,
}
