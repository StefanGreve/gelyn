using System.Net;
using System.Text;

using Gelyn.Core;
using Gelyn.Core.Context;
using Gelyn.Core.Internals;
using Gelyn.Model.Options;

namespace Gelyn.Components;

/// <summary>
///     The block rendered at the bottom of every page.
/// </summary>
internal static class FooterComponent
{
    /// <summary>
    ///     Renders the block.
    /// </summary>
    /// <param name="context">
    ///     The context the page is rendered against.
    /// </param>
    /// <returns>
    ///     The HTML of the fragment.
    /// </returns>
    public static string Render(RenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var footer = new StringBuilder();

        string version = WebUtility.HtmlEncode(Tool.Version);
        string revised = string.Empty;

        if (context.Page.Revised is DateOnly date)
        {
            string machine = date.ToString(DateFormat.Machine, Tool.Culture);
            revised = $""" - last revised <time datetime="{machine}">{date.ToString(DateFormat.Display, Tool.Culture)}</time>""";
        }

        footer.AppendLine("<footer>");
        footer.AppendLinks(context);
        footer.AppendLine($"<span>Built with {Tool.Name} v{version}{revised}</span>");
        footer.AppendLine("</footer>");

        return footer.ToString();
    }

    #region Helpers

    private static void AppendLinks(this StringBuilder footer, RenderContext context)
    {
        SiteOptions options = context.Options;
        var links = new List<(string Label, string Href)>(options.FooterLinks.Count + 2);

        foreach (FooterLink link in options.FooterLinks)
        {
            Uri url = link.Url ?? throw new InvalidOperationException("A configured footer link always declares a URL.");
            links.Add((link.Label, url.AbsoluteUri));
        }

        // Linked under the conditions SiteBuilder writes each document under, so that no entry leads to a 404.
        if (options.BaseUrl is Uri baseUrl)
        {
            string prefix = UrlUtils.ResolveBasePath(baseUrl);

            if (options.EnableFeed && context.Documents.HasFeedEntries)
                links.Add(("Feed", StringUtils.JoinSegments(prefix, [], AtomFeed.FileName, '/')));

            if (options.EnableSitemap)
                links.Add(("Sitemap", StringUtils.JoinSegments(prefix, [], Sitemap.FileName, '/')));
        }

        if (links.Count == 0) return;

        footer.AppendLine("<ul>");

        foreach ((string label, string href) in links)
            footer.AppendLine($"""<li><a href="{WebUtility.HtmlEncode(href)}">{WebUtility.HtmlEncode(label)}</a></li>""");

        footer.AppendLine("</ul>");
    }

    #endregion
}
