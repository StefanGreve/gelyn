using Gelyn.Model;
using Gelyn.Model.Options;

namespace Gelyn.Core.Context;

/// <summary>
///     Everything a renderer needs to lay out one page of the site.
/// </summary>
internal sealed record RenderContext
{
    /// <summary>
    ///     The configuration snapshot the whole build is rendered against.
    /// </summary>
    public required SiteOptions Options { get; init; }

    /// <summary>
    ///     The pages the site navigation links, landing page first.
    /// </summary>
    public required IReadOnlyList<ContentPage> Navigation { get; init; }

    /// <summary>
    ///     Every page the site is built from, landing page first.
    /// </summary>
    public required IReadOnlyList<ContentPage> Documents { get; init; }

    /// <summary>
    ///     The page currently being rendered.
    /// </summary>
    public required ContentPage Page { get; init; }
}
