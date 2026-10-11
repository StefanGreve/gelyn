using System.Collections.Generic;

namespace Gelyn.Model;

/// <summary>
///     The resources that apply to a page, outermost first.
/// </summary>
internal sealed record Cascade
{
    /// <summary>
    ///     The root-relative URLs of the stylesheets.
    /// </summary>
    public required IReadOnlyList<string> Stylesheets { get; init; }

    /// <summary>
    ///     The root-relative URLs of the scripts.
    /// </summary>
    public required IReadOnlyList<string> Scripts { get; init; }
}
