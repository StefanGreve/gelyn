namespace Gelyn.Core.Context;

/// <summary>
///     What one level of the content walk inherits from the level above it.
/// </summary>
internal sealed record ScanContext
{
    /// <summary>
    ///     The output-relative segments leading to the directory being walked, empty at the content root.
    /// </summary>
    public required IReadOnlyList<string> Prefix { get; init; }

    /// <summary>
    ///     The root-relative URLs of the stylesheets declared above the directory being walked, outermost first,
    ///     after those of the configured theme, which the scan seeds the content root with.
    /// </summary>
    public required IReadOnlyList<string> Stylesheets { get; init; }

    /// <summary>
    ///     The root-relative URLs of the scripts declared above the directory being walked, outermost first,
    ///     after those of the configured theme, which the scan seeds the content root with.
    /// </summary>
    public required IReadOnlyList<string> Scripts { get; init; }

    /// <summary>
    ///     The root-relative URL of the nearest icon declared above the directory being walked, or
    ///     <see langword="null"/> when none is.
    /// </summary>
    public required string? Favicon { get; init; }

    /// <summary>
    ///     The root-relative URL of the directory being walked without a trailing slash, which opens with the
    ///     path of the configured base URL and percent-encodes every directory name after it. Empty at the
    ///     content root of a site served from the root of a domain.
    /// </summary>
    public required string HrefPrefix { get; init; }

    /// <summary>
    ///     The name patterns an entry is left out for. Fixed for the whole scan.
    /// </summary>
    public required IReadOnlyList<string> Excluded { get; init; }
}
