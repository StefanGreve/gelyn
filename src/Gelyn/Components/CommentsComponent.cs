using System.Net;

using Gelyn.Core.Context;
using Gelyn.Core.Internals.Primitives;
using Gelyn.Model;
using Gelyn.Model.Options;

namespace Gelyn.Components;

/// <summary>
///     The comment thread rendered below the content of a page that asks for one.
/// </summary>
internal static class CommentsComponent
{
    private const string Client = "https://giscus.app/client.js";
    private const string Mapping = "pathname";
    private const string Theme = "preferred_color_scheme";

    /// <summary>
    ///     Renders the thread once for the build.
    /// </summary>
    /// <param name="options">
    ///     The configuration snapshot the build is rendered against, which may configure a backend.
    /// </param>
    /// <returns>
    ///     The HTML of the section holding the giscus client, or an empty string for a site that configures no
    ///     backend.
    /// </returns>
    public static string RenderThread(SiteOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Giscus is not GiscusOptions giscus)
            return string.Empty;

        return $"""
            <section class="giscus" aria-label="Comments">
            <script src="{Client}"
                    data-repo="{WebUtility.HtmlEncode(giscus.Repo)}"
                    data-repo-id="{WebUtility.HtmlEncode(giscus.RepoId)}"
                    data-category="{WebUtility.HtmlEncode(giscus.Category)}"
                    data-category-id="{WebUtility.HtmlEncode(giscus.CategoryId)}"
                    data-mapping="{Mapping}"
                    data-strict="1"
                    data-reactions-enabled="1"
                    data-emit-metadata="0"
                    data-input-position="bottom"
                    data-theme="{Theme}"
                    data-loading="lazy"
                    crossorigin="anonymous"
                    async></script>
            </section>
        """;
    }

    /// <summary>
    ///     Writes the thread for a page that carries one.
    /// </summary>
    /// <param name="context">
    ///     What the layout shares between the pages of the build, the thread included.
    /// </param>
    /// <param name="page">
    ///     The page being rendered, which carries the thread when the site configures a backend and the page,
    ///     which is not the not-found page, enables comments.
    /// </param>
    /// <param name="buffer">
    ///     The buffer the page is composed into.
    /// </param>
    public static void Render(LayoutContext context, ContentPage page, PageBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(buffer);

        if (page is { EnableComments: true, IsNotFoundPage: false })
            buffer.Append(context.Comments.Span);
    }
}
