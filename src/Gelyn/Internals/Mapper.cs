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
    ///     Projects a located file onto the page rendered from it.
    /// </summary>
    /// <param name="item">
    ///     The file the scan located, which supplies the output path, the URL and the title to fall back to.
    /// </param>
    /// <param name="document">
    ///     The front matter and the HTML body read out of that file.
    /// </param>
    /// <returns>
    ///     The new page, titled by the front matter when it declares a title and by
    ///     <see cref="ContentItem.FallbackTitle"/> when it does not, which is what keeps
    ///     <see cref="ContentPage.Title"/> free of nulls.
    /// </returns>
    internal static ContentPage ToContentPage(this ContentItem item, RenderedMarkdown document) =>
        new()
        {
            OutputPath = item.OutputPath,
            Href = item.Href,
            Title = document.FrontMatter?.Title ?? item.FallbackTitle,
            Description = document.FrontMatter?.Description,
            Date = document.FrontMatter?.Date,
            Html = document.Html,
            InNavigation = item.InNavigation,
        };

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
