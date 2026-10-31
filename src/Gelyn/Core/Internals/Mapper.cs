using Gelyn.Core.Context;
using Gelyn.Model;
using Gelyn.Model.Options;

namespace Gelyn.Core.Internals;

/// <summary>
///     Extension methods for the model types the generator passes around.
/// </summary>
internal static class Mapper
{
    /// <summary>
    ///     Projects a located file onto the page rendered from it.
    /// </summary>
    /// <param name="item">
    ///     The file the scan located.
    /// </param>
    /// <param name="document">
    ///     The result of rendering that file.
    /// </param>
    /// <returns>
    ///     The new page, titled by the front matter when it declares a title and by
    ///     <see cref="ContentItem.FallbackTitle"/> when it does not.
    /// </returns>
    internal static ContentPage ToContentPage(this ContentItem item, RenderedMarkdown document) =>
        new()
        {
            SourcePath = item.SourcePath,
            OutputPath = item.OutputPath,
            Href = item.Href,
            Title = document.FrontMatter?.Title ?? item.FallbackTitle,
            Description = document.FrontMatter?.Description,
            Date = document.FrontMatter?.Date,
            EnableMathRendering = document.FrontMatter?.EnableMathRendering ?? false,
            EnableDiagramRendering = document.FrontMatter?.EnableDiagramRendering ?? false,
            Html = document.Html,
            InNavigation = item.InNavigation,
            InFeed = document.FrontMatter?.EnableFeed ?? false,
            Stylesheets = item.Stylesheets,
            Scripts = item.Scripts,
            Favicon = item.Favicon,
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
    /// <param name="documents">
    ///     Every page the site is built from, landing page first.
    /// </param>
    /// <returns>
    ///     The new context.
    /// </returns>
    internal static RenderContext ToRenderContext(
        this ContentPage page,
        SiteOptions options,
        IReadOnlyList<ContentPage> navigation,
        IReadOnlyList<ContentPage> documents) =>
        new()
        {
            Options = options,
            Navigation = navigation,
            Documents = documents,
            Page = page,
        };
}
