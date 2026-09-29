using System;
using System.Collections.Generic;

using Gelyn.Model;
using Gelyn.Model.Options;

namespace Gelyn.Internals;

/// <summary>
///     Conversions between the model types the generator passes around.
/// </summary>
internal static class Mapper
{
    /// <summary>
    ///     Builds the context a single page is rendered against.
    /// </summary>
    /// <param name="page">
    ///     The page to render.
    /// </param>
    /// <param name="options">
    ///     The site configuration.
    /// </param>
    /// <param name="navigation">
    ///     The pages the site navigation links, landing page first.
    /// </param>
    /// <param name="generatedAt">
    ///     The UTC date the build started.
    /// </param>
    /// <returns>
    ///     The new context.
    /// </returns>
    internal static RenderContext ToRenderContext(
        this ContentPage page,
        SiteOptions options,
        IReadOnlyList<ContentPage> navigation,
        DateOnly generatedAt) =>
        new()
        {
            Options = options,
            Navigation = navigation,
            Page = page,
            GeneratedAt = generatedAt,
        };
}
