using System.Net;

using Gelyn.Components;
using Gelyn.Model;
using Gelyn.Model.Options;

namespace Gelyn.Core.Context;

/// <summary>
///     Everything the layout shares between the pages of one build, rendered once.
/// </summary>
internal sealed class LayoutContext
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="LayoutContext"/> class.
    /// </summary>
    /// <param name="options">
    ///     The configuration snapshot the build is rendered against.
    /// </param>
    /// <param name="pages">
    ///     Every page the site is built from, in scan order, the landing page first.
    /// </param>
    public LayoutContext(SiteOptions options, IReadOnlyList<ContentPage> pages)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(pages);

        this.Options = options;
        this.Documents = new DocumentCatalog(pages);
        this.Banner = HeaderComponent.Render(options, [.. pages.Where(static page => page.InNavigation)]);
        this.Footer = FooterComponent.Render(options, this.Documents);
        this.Language = WebUtility.HtmlEncode(options.Language);
        this.FeedLink = HeadComponent.RenderFeedLink(options, this.Documents);
        this.Comments = CommentsComponent.RenderThread(options);
    }

    /// <summary>
    ///     The configuration snapshot the build is rendered against.
    /// </summary>
    public SiteOptions Options { get; }

    /// <summary>
    ///     What the build knows about every page at once.
    /// </summary>
    public DocumentCatalog Documents { get; }

    /// <summary>
    ///     The banner, with no entry marked, and where in it each entry takes its mark.
    /// </summary>
    public Banner Banner { get; }

    /// <summary>
    ///     The footer, with no revision fragment, and where in it a page's fragment goes.
    /// </summary>
    public Footer Footer { get; }

    /// <summary>
    ///     The language of the site, encoded for the attribute of the root element.
    /// </summary>
    public string Language { get; }

    /// <summary>
    ///     The link that announces the feed, or an empty string while the build writes none.
    /// </summary>
    public string FeedLink { get; }

    /// <summary>
    ///     The comment thread of a site that configures a backend, or an empty string.
    /// </summary>
    public string Comments { get; }
}
