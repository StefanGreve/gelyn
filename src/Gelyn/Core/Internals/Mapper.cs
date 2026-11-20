using Gelyn.Model;

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
            Revised = document.FrontMatter?.Revised ?? document.FrontMatter?.Date,
            EnableMathRendering = document.FrontMatter?.EnableMathRendering ?? false,
            EnableDiagramRendering = document.FrontMatter?.EnableDiagramRendering ?? false,
            EnableSyntaxHighlighting = document.FrontMatter?.EnableSyntaxHighlighting ?? false,
            EnableComments = document.FrontMatter?.EnableComments ?? false,
            Body = Tool.Encoding.GetBytes(document.Html),
            InNavigation = item.InNavigation,
            IsNotFoundPage = item.IsNotFoundPage,
            InFeed = document.FrontMatter?.EnableFeed ?? false,
            Stylesheets = item.Stylesheets,
            Scripts = item.Scripts,
            Favicon = item.Favicon,
            UnknownKeys = document.UnknownKeys,
        };
}
