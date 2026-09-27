using System;
using System.Collections.Generic;

using Gelyn.Model.Options;

namespace Gelyn.Model;

/// <summary>
///     Everything a renderer needs to lay out one page of the site.
/// </summary>
public sealed record RenderContext
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
    ///     The page currently being rendered.
    /// </summary>
    public required ContentPage Page { get; init; }

    /// <summary>
    ///     The UTC date the build started.
    /// </summary>
    public required DateOnly GeneratedAt { get; init; }
}
