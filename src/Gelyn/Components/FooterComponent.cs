using System.Net;
using System.Text;

using Gelyn.Core;
using Gelyn.Core.Internals;
using Gelyn.Model.Options;

namespace Gelyn.Components;

/// <summary>
///     The block rendered at the bottom of every page.
/// </summary>
internal static class FooterComponent
{
    /// <summary>
    ///     Renders the block once for the build, cut where a page's revision fragment goes.
    /// </summary>
    /// <param name="options">
    ///     The configuration snapshot the build is rendered against, which declares the links.
    /// </param>
    /// <param name="documents">
    ///     What the build knows about every page at once, which decides whether the feed is linked.
    /// </param>
    /// <returns>
    ///     The footer.
    /// </returns>
    public static Footer Render(SiteOptions options, DocumentCatalog documents)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(documents);

        var html = new StringBuilder("<footer>");

        html.AppendLinks(options, documents);
        html.Append($"<span>Built with {Tool.Name} v{WebUtility.HtmlEncode(Tool.Version)}");

        int offset = html.Length;

        html.Append("</span></footer>");

        return new Footer
        {
            Html = html.ToString(),
            Offset = offset
        };
    }

    #region Helpers

    private static void AppendLinks(this StringBuilder footer, SiteOptions options, DocumentCatalog documents)
    {
        var links = new List<(string Label, string Href)>(options.FooterLinks.Count + 2);

        foreach (FooterLink link in options.FooterLinks)
        {
            if (link is not { Label: string label, Url: Uri url })
                throw new InvalidOperationException("A configured footer link always declares a label and a URL.");

            links.Add((label, url.AbsoluteUri));
        }

        // Linked under the conditions SiteBuilder writes each document under, so that no entry leads to a 404.
        if (options.BaseUrl is Uri baseUrl)
        {
            string prefix = UrlUtils.ResolveBasePath(baseUrl);

            if (options.EnableFeed && documents.HasFeedEntries)
                links.Add(("Feed", $"{prefix}/{AtomFeed.FileName}"));

            if (options.EnableSitemap)
                links.Add(("Sitemap", $"{prefix}/{Sitemap.FileName}"));
        }

        if (links.Count == 0) return;

        footer.Append("<ul>");

        foreach ((string label, string href) in links)
            footer.Append($"""<li><a href="{WebUtility.HtmlEncode(href)}">{WebUtility.HtmlEncode(label)}</a></li>""");

        footer.Append("</ul>");
    }

    #endregion
}
