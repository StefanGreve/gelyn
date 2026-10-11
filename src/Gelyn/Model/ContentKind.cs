namespace Gelyn.Model;

/// <summary>
///     How the build treats a file the scan located.
/// </summary>
internal enum ContentKind
{
    /// <summary>
    ///     Markdown, rendered into an HTML document.
    /// </summary>
    Page,

    /// <summary>
    ///     A stylesheet, copied like any other file and additionally linked by the pages it applies to.
    /// </summary>
    Stylesheet,

    /// <summary>
    ///     A script, copied like any other file and additionally linked by the pages it applies to.
    /// </summary>
    Script,

    /// <summary>
    ///     Anything else, copied to the output directory byte for byte.
    /// </summary>
    MiscFile,
}
