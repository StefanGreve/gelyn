namespace Gelyn.Core.Context;

/// <summary>
///     What one level of the content walk inherits from the level above it.
/// </summary>
/// <remarks>
///     Two kinds of value sit here and the distinction is worth keeping in mind when the walk is changed:
///     <see cref="HrefPrefix"/> and <see cref="Excluded"/> are fixed for the whole scan, while the rest are
///     recomputed per directory and handed down with a <c>with</c> expression. A directory therefore reads
///     its own state and never writes to a parent's.
/// </remarks>
internal sealed record ScanContext
{
    /// <summary>
    ///     The output-relative segments leading to the directory being walked, empty at the content root.
    /// </summary>
    public required IReadOnlyList<string> Prefix { get; init; }

    /// <summary>
    ///     The root-relative URLs of the stylesheets declared at or above the directory, outermost first.
    /// </summary>
    public required IReadOnlyList<string> Stylesheets { get; init; }

    /// <summary>
    ///     The root-relative URLs of the scripts declared at or above the directory, outermost first.
    /// </summary>
    public required IReadOnlyList<string> Scripts { get; init; }

    /// <summary>
    ///     The root-relative URL of the nearest icon declared at or above the directory, or
    ///     <see langword="null"/> when none is.
    /// </summary>
    public required string? Favicon { get; init; }

    /// <summary>
    ///     The path of the configured base URL, or an empty string when the site is served from the root of
    ///     a domain. Fixed for the whole scan.
    /// </summary>
    public required string HrefPrefix { get; init; }

    /// <summary>
    ///     The name patterns an entry is left out for. Fixed for the whole scan.
    /// </summary>
    public required IReadOnlyList<string> Excluded { get; init; }
}
