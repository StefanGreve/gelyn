using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;

using Gelyn.Components;
using Gelyn.Core;
using Gelyn.Core.Internals;
using Gelyn.Model;
using Gelyn.Model.Options;

namespace Gelyn.Tests.Components;

/// <summary>
///     Unit tests for the <see cref="FooterComponent"/> class.
/// </summary>
[Category(Categories.Internal)]
public class FooterComponentTests
{
    #region Link List Tests

    /// <summary>
    ///     Verifies that a site configuring no link and publishing neither document writes no list at all,
    ///     never an empty one, and keeps the colophon.
    /// </summary>
    [Test]
    public async Task Render_Test_NothingConfigured_WritesNoList()
    {
        // Act
        string footer = Render(new SiteOptions());

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(footer).DoesNotContain("<ul>");
            await Assert.That(footer).Contains("<span>Built with Gelyn v");
        }
    }

    /// <summary>
    ///     Verifies that configured links are listed in the order they are declared in.
    /// </summary>
    [Test]
    public async Task Render_Test_ConfiguredLinks_KeepTheirDeclaredOrder()
    {
        // Arrange
        var options = new SiteOptions
        {
            FooterLinks =
            [
                Link("Mastodon", "https://mastodon.social/@octocat"),
                Link("GitHub", "https://github.com/octocat"),
            ],
        };

        // Act
        string footer = Render(options);

        // Assert
        int mastodon = footer.IndexOf(">Mastodon<", StringComparison.Ordinal);
        int gitHub = footer.IndexOf(">GitHub<", StringComparison.Ordinal);

        using (Assert.Multiple())
        {
            await Assert.That(mastodon).IsGreaterThanOrEqualTo(0);
            await Assert.That(gitHub).IsGreaterThan(mastodon);
        }
    }

    /// <summary>
    ///     Verifies that a label and a URL are escaped for the content and the attribute they are written into.
    /// </summary>
    [Test]
    public async Task Render_Test_ConfiguredLink_IsEscaped()
    {
        // Arrange
        var options = new SiteOptions { FooterLinks = [Link("Ann & Bob", "https://example.com/?a=1&b=2")] };

        // Act
        string footer = Render(options);

        // Assert
        await Assert.That(footer).Contains("""<li><a href="https://example.com/?a=1&amp;b=2">Ann &amp; Bob</a></li>""");
    }

    /// <summary>
    ///     Verifies that the list precedes the colophon in the document, leaving their painted order to the
    ///     stylesheet.
    /// </summary>
    [Test]
    public async Task Render_Test_List_PrecedesTheColophon()
    {
        // Arrange
        var options = new SiteOptions { FooterLinks = [Link("GitHub", "https://github.com/octocat")] };

        // Act
        string footer = Render(options, revised: "2026-10-25");

        // Assert
        int list = footer.IndexOf("</ul>", StringComparison.Ordinal);
        int colophon = footer.IndexOf("<span>", StringComparison.Ordinal);

        using (Assert.Multiple())
        {
            await Assert.That(list).IsGreaterThanOrEqualTo(0);
            await Assert.That(colophon).IsGreaterThan(list);
        }
    }

    #endregion // Link List Tests

    #region Generated Entry Tests

    /// <summary>
    ///     Verifies that the feed and then the sitemap are appended after the configured links when the build
    ///     publishes both.
    /// </summary>
    [Test]
    public async Task Render_Test_FeedAndSitemap_FollowTheConfiguredLinks()
    {
        // Arrange
        var options = new SiteOptions
        {
            BaseUrl = new Uri("https://example.com", UriKind.Absolute),
            EnableFeed = true,
            EnableSitemap = true,
            FooterLinks = [Link("GitHub", "https://github.com/octocat")],
        };

        // Act
        string footer = Render(options, hasFeedEntry: true);

        // Assert
        int configured = footer.IndexOf(""">GitHub</a>""", StringComparison.Ordinal);
        int feed = footer.IndexOf("""<a href="/feed.xml">Feed</a>""", StringComparison.Ordinal);
        int sitemap = footer.IndexOf("""<a href="/sitemap.xml">Sitemap</a>""", StringComparison.Ordinal);

        using (Assert.Multiple())
        {
            await Assert.That(configured).IsGreaterThanOrEqualTo(0);
            await Assert.That(feed).IsGreaterThan(configured);
            await Assert.That(sitemap).IsGreaterThan(feed);
        }
    }

    /// <summary>
    ///     Verifies that neither document is linked without a base URL, the build writing neither file then.
    /// </summary>
    [Test]
    public async Task Render_Test_GeneratedEntries_AreLeftOutWithoutABaseUrl()
    {
        // Arrange
        var options = new SiteOptions { EnableFeed = true, EnableSitemap = true };

        // Act
        string footer = Render(options, hasFeedEntry: true);

        // Assert
        await Assert.That(footer).DoesNotContain("<ul>");
    }

    /// <summary>
    ///     Verifies that a feed asked for by a site whose pages carry no entry is left out, the build writing
    ///     no document for the link to address.
    /// </summary>
    [Test]
    public async Task Render_Test_Feed_IsLeftOutWithoutAnEntry()
    {
        // Arrange
        var options = new SiteOptions
        {
            BaseUrl = new Uri("https://example.com", UriKind.Absolute),
            EnableFeed = true,
            EnableSitemap = true,
        };

        // Act
        string footer = Render(options);

        // Assert
        await Assert.That(footer)
            .Contains("""<a href="/sitemap.xml">Sitemap</a>""")
            .And.DoesNotContain("feed.xml");
    }

    /// <summary>
    ///     Verifies that a document whose switch is off is left out, even with a base URL and a feed entry,
    ///     the build writing no file for the link to address, while the other document stays linked.
    /// </summary>
    [Test]
    [Arguments(false, true, "/feed.xml", "/sitemap.xml")]
    [Arguments(true, false, "/sitemap.xml", "/feed.xml")]
    public async Task Render_Test_GeneratedEntry_IsLeftOutWhenItsSwitchIsOff(bool enableFeed, bool enableSitemap, string absent, string present)
    {
        // Arrange
        var options = new SiteOptions
        {
            BaseUrl = new Uri("https://example.com", UriKind.Absolute),
            EnableFeed = enableFeed,
            EnableSitemap = enableSitemap,
        };

        // Act
        string footer = Render(options, hasFeedEntry: true);

        // Assert
        await Assert.That(footer)
            .Contains($"""<a href="{present}">""")
            .And.DoesNotContain($"""<a href="{absent}">""");
    }

    /// <summary>
    ///     Verifies that both generated entries address their document below the path the base URL names,
    ///     with or without a trailing slash on that URL.
    /// </summary>
    [Test]
    [Arguments("https://example.com/gelyn/docs")]
    [Arguments("https://example.com/gelyn/docs/")]
    public async Task Render_Test_BaseUrlPath_PrefixesTheGeneratedHrefs(string baseUrl)
    {
        // Arrange
        var options = new SiteOptions
        {
            BaseUrl = new Uri(baseUrl, UriKind.Absolute),
            EnableFeed = true,
            EnableSitemap = true,
        };

        // Act
        string footer = Render(options, hasFeedEntry: true);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(footer).Contains("""<a href="/gelyn/docs/feed.xml">Feed</a>""");
            await Assert.That(footer).Contains("""<a href="/gelyn/docs/sitemap.xml">Sitemap</a>""");
        }
    }

    #endregion // Generated Entry Tests

    #region Revision Tests

    /// <summary>
    ///     Verifies that a revised page dates the colophon, in both forms of the date, right before its closing tag.
    /// </summary>
    [Test]
    public async Task CutFor_Test_RevisedPage_DatesTheColophon()
    {
        // Act
        string footer = Render(new SiteOptions(), revised: "2026-10-25");

        // Assert
        await Assert.That(footer).Contains(""" - last revised <time datetime="2026-10-25">Oct 25, 2026</time></span>""");
    }

    /// <summary>
    ///     Verifies that a page declaring no revision leaves the colophon undated.
    /// </summary>
    [Test]
    public async Task CutFor_Test_UnrevisedPage_LeavesTheColophonUndated()
    {
        // Act
        string footer = Render(new SiteOptions());

        // Assert
        await Assert.That(footer).DoesNotContain("last revised");
    }

    #endregion // Revision Tests

    #region Helpers

    private static FooterLink Link(string label, string url) =>
        new() { Label = label, Url = new Uri(url, UriKind.Absolute) };

    private static string Render(SiteOptions options, string? revised = null, bool hasFeedEntry = false)
    {
        var page = new ContentPage
        {
            SourcePath = "index.md",
            OutputPath = "index.html",
            Href = "/index.html",
            Title = "Home",
            Description = null,
            Date = null,
            Revised = revised is null ? null : DateOnly.Parse(revised, CultureInfo.InvariantCulture),
            EnableMathRendering = false,
            EnableDiagramRendering = false,
            EnableSyntaxHighlighting = false,
            EnableComments = false,
            Html = "<p>Home</p>",
            InNavigation = true,
            IsNotFoundPage = false,
            InFeed = false,
            Stylesheets = [],
            Scripts = [],
            Favicon = null,
            UnknownKeys = [],
        };

        // The entry sits on a second page, so the link is shown to follow the site and not the page.
        IReadOnlyList<ContentPage> documents = hasFeedEntry
            ? [page, page with { Href = "/post.html", InFeed = true, Date = new DateOnly(2026, 1, 1) }]
            : [page];

        // Written the way PageLayout writes the footer, around the page's own revision fragment.
        using var buffer = new PageBuffer();
        FooterComponent.Render(options, new DocumentCatalog(documents)).CutFor(page).WriteTo(buffer);

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    #endregion
}
