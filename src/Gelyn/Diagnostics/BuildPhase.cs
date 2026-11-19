namespace Gelyn.Diagnostics;

/// <summary>
///     The phases of a build that a debug counter reports a duration for, in the order they are reported.
/// </summary>
internal enum BuildPhase
{
    /// <summary>
    ///     Locating the source files.
    /// </summary>
    Scan,

    /// <summary>
    ///     Reading the pages and rendering their Markdown.
    /// </summary>
    Render,

    /// <summary>
    ///     Deleting the output the build no longer publishes.
    /// </summary>
    Remove,

    /// <summary>
    ///     Composing the rendered pages into documents.
    /// </summary>
    Compose,

    /// <summary>
    ///     Writing the output, the generated documents and the copies included.
    /// </summary>
    Write,
}
