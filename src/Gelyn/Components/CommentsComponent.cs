using System.Net;

using Gelyn.Core.Context;
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
    ///     Renders the thread.
    /// </summary>
    /// <param name="context">
    ///     The context the page is rendered against.
    /// </param>
    /// <returns>
    ///     The HTML of the section holding the giscus client, or an empty string unless the site configures a
    ///     backend and the page, which is not the not-found page, enables comments.
    /// </returns>
    public static string Render(RenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Options.Giscus is not GiscusOptions giscus || context.Page is { EnableComments: false } or { IsNotFoundPage: true })
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
}
