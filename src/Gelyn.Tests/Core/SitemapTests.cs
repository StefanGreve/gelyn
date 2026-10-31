using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

using Gelyn.Core;
using Gelyn.Model;
using Gelyn.Model.Options;

namespace Gelyn.Tests.Core;

/// <summary>
///     Unit tests for the <see cref="Sitemap"/> class.
/// </summary>
public class SitemapTests
{
    private static readonly XNamespace Sitemaps = "http://www.sitemaps.org/schemas/sitemap/0.9";

    #region Gate Tests

    /// <summary>
    ///     Verifies that no document is produced while the configuration declares no base URL, every location
    ///     in a sitemap being absolute and only the base URL supplying an origin to resolve one against.
    /// </summary>
    [Test]
    public async Task Render_Test_AbsentBaseUrl_WritesNothing()
    {
        // Arrange
        var options = new SiteOptions();

        // Act
        byte[] sitemap = Sitemap.Render(options, [Page("index.html", "2026-01-01")]);

        // Assert
        await Assert.That(sitemap).IsEmpty();
    }

    #endregion // Gate Tests

    #region Membership Tests

    /// <summary>
    ///     Verifies that every page is listed, a sitemap being an inventory of what the site publishes and not
    ///     a selection from it, so that no front matter key decides membership.
    /// </summary>
    /// <remarks>
    ///     The three pages differ in each of the ways a page can: one is undated, which makes it a draft to
    ///     every listing, one stays out of the navigation, and none opts into the feed.
    /// </remarks>
    [Test]
    public async Task Render_Test_EveryPage_IsListed()
    {
        // Arrange
        ContentPage[] pages =
        [
            Page("index.html", "2026-01-01"),
            Page("draft.html", date: null),
            Page("hidden.html", "2026-02-01") with { InNavigation = false },
        ];

        // Act
        string locations = Locations(Render(Options(), pages));

        // Assert
        await Assert.That(locations).IsEqualTo(
            "https://example.com/index.html,https://example.com/draft.html,https://example.com/hidden.html");
    }

    /// <summary>
    ///     Verifies that a page declaring neither date is listed without a revision date, the element being
    ///     optional and there being nothing to put in it.
    /// </summary>
    [Test]
    public async Task Render_Test_DatelessPage_CarriesNoLastmod()
    {
        // Act
        XElement url = Urls(Render(Options(), [Page("draft.html", date: null)])).Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(Value(url, "loc")).IsEqualTo("https://example.com/draft.html");
            await Assert.That(Value(url, "lastmod")).IsNull();
        }
    }

    #endregion // Membership Tests

    #region Document Tests

    /// <summary>
    ///     Verifies that the whole document is what the protocol asks for, down to the indentation and the
    ///     order of the elements inside a location.
    /// </summary>
    /// <remarks>
    ///     Line endings are normalized before the comparison, because
    ///     <see cref="System.Xml.XmlWriterSettings.NewLineChars"/> defaults to the line ending of the platform
    ///     the build ran on, as every component writing HTML does. The structure is what is under test here.
    /// </remarks>
    [Test]
    public async Task Render_Test_Document_MatchesTheProtocol()
    {
        // Arrange
        const string expected = """
            <?xml version="1.0" encoding="utf-8"?>
            <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
              <url>
                <loc>https://example.com/index.html</loc>
                <lastmod>2026-01-01</lastmod>
              </url>
              <url>
                <loc>https://example.com/about.html</loc>
              </url>
            </urlset>
            """;

        ContentPage[] pages = [Page("index.html", "2026-01-01"), Page("about.html", date: null)];

        // Act
        string xml = Xml(Options(), pages);

        // Assert
        await Assert.That(xml.ReplaceLineEndings("\n")).IsEqualTo(expected);
    }

    /// <summary>
    ///     Verifies that the root element is the one the protocol names, in the namespace it names, which is
    ///     what a consumer recognizes the document by.
    /// </summary>
    [Test]
    public async Task Render_Test_RootElement_DeclaresTheProtocolNamespace()
    {
        // Act
        XElement urlset = Render(Options(), [Page("index.html", "2026-01-01")]);

        // Assert
        await Assert.That(urlset.Name).IsEqualTo(Sitemaps + "urlset");
    }

    /// <summary>
    ///     Verifies that the declaration states UTF-8, which is what the returned bytes are encoded in.
    /// </summary>
    /// <remarks>
    ///     Building the document over a writer of strings would have declared UTF-16 here, that being the
    ///     encoding of such a writer, and left the declaration contradicting the file written from it.
    /// </remarks>
    [Test]
    public async Task Render_Test_Declaration_StatesUtf8()
    {
        // Act
        string xml = Xml(Options(), [Page("index.html", "2026-01-01")]);

        // Assert
        await Assert.That(xml).StartsWith("""<?xml version="1.0" encoding="utf-8"?>""");
    }

    /// <summary>
    ///     Verifies that an ampersand in a location is escaped, and reads back as the URL that went in.
    /// </summary>
    /// <remarks>
    ///     It is the one character on the protocol's escape table that survives URL encoding, being legal in
    ///     a path, so it reaches the writer as itself and would otherwise open an entity reference.
    /// </remarks>
    [Test]
    public async Task Render_Test_AmpersandInALocation_IsEscaped()
    {
        // Act
        string xml = Xml(Options(), [Page("tom&jerry.html", "2026-01-01")]);
        XElement url = Urls(XElement.Parse(xml)).Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(xml).Contains("<loc>https://example.com/tom&amp;jerry.html</loc>");
            await Assert.That(Value(url, "loc")).IsEqualTo("https://example.com/tom&jerry.html");
        }
    }

    /// <summary>
    ///     Verifies that a base URL addressing the root of a domain is handled as one, so that a location does
    ///     not address a directory the site has no such thing as.
    /// </summary>
    [Test]
    [Arguments("https://example.com")]
    [Arguments("https://example.com/")]
    public async Task Render_Test_DomainRootBaseUrl_AddressesTheRoot(string baseUrl)
    {
        // Act
        XElement url = Urls(Render(Options(baseUrl), [Page("index.html", "2026-01-01")])).Single();

        // Assert
        await Assert.That(Value(url, "loc")).IsEqualTo("https://example.com/index.html");
    }

    /// <summary>
    ///     Verifies that a base URL carrying a path keeps that path once and not twice, which is the case a
    ///     site deployed below a domain root is served under.
    /// </summary>
    /// <remarks>
    ///     The href already carries the prefix, so resolving it against the base URL as a relative reference
    ///     would have doubled it.
    /// </remarks>
    [Test]
    public async Task Render_Test_BaseUrlWithAPath_KeepsThePathPrefixOnce()
    {
        // Arrange
        ContentPage page = Page("index.html", "2026-01-01") with { Href = "/gelyn/index.html" };

        // Act
        XElement url = Urls(Render(Options("https://example.com/gelyn"), [page])).Single();

        // Assert
        await Assert.That(Value(url, "loc")).IsEqualTo("https://example.com/gelyn/index.html");
    }

    /// <summary>
    ///     Verifies that the revision date is the date the page was revised and not the date it was published,
    ///     which are the two the front matter separates.
    /// </summary>
    [Test]
    public async Task Render_Test_Lastmod_StatesTheRevision()
    {
        // Arrange
        ContentPage page = Page("post.html", "2026-01-01", revised: "2026-03-04");

        // Act
        XElement url = Urls(Render(Options(), [page])).Single();

        // Assert
        await Assert.That(Value(url, "lastmod")).IsEqualTo("2026-03-04");
    }

    #endregion // Document Tests

    #region Determinism Tests

    /// <summary>
    ///     Verifies that the same pages render to the same bytes, so that a build of unchanged content leaves
    ///     the sitemap as the build before it wrote it.
    /// </summary>
    [Test]
    public async Task Render_Test_SameInput_ProducesIdenticalBytes()
    {
        // Arrange
        ContentPage[] pages = [Page("index.html", "2026-01-01")];

        // Act and Assert
        // Compared as base64, which is sensitive to the order of the bytes where IsEquivalentTo is not.
        string first = Convert.ToBase64String(Sitemap.Render(Options(), pages));
        string second = Convert.ToBase64String(Sitemap.Render(Options(), pages));

        await Assert.That(first).IsEqualTo(second);
    }

    #endregion // Determinism Tests

    #region Helpers

    // Only the base URL is read, every other setting reaching the document by way of no element at all.
    private static SiteOptions Options(string baseUrl = "https://example.com") =>
        new()
        {
            BaseUrl = new Uri(baseUrl, UriKind.Absolute),
        };

    private static ContentPage Page(string outputPath, string? date, string? revised = null) =>
        new()
        {
            SourcePath = Path.ChangeExtension(outputPath, ".md"),
            OutputPath = outputPath,
            Href = $"/{outputPath}",
            Title = outputPath,
            Description = null,
            Date = Parse(date),

            // Resolved the way Mapper resolves it, so that a case declaring no revision carries the fallback
            // the renderer is handed in a build and not a null the renderer never sees.
            Revised = Parse(revised) ?? Parse(date),
            EnableMathRendering = false,
            EnableDiagramRendering = false,
            Html = string.Empty,
            InNavigation = true,
            InFeed = false,
            Stylesheets = [],
            Scripts = [],
            Favicon = null,
        };

    private static DateOnly? Parse(string? date) =>
        date is null ? null : DateOnly.Parse(date, CultureInfo.InvariantCulture);

    private static string Xml(SiteOptions options, IReadOnlyList<ContentPage> pages) =>
        Encoding.UTF8.GetString(Sitemap.Render(options, pages));

    private static XElement Render(SiteOptions options, IReadOnlyList<ContentPage> pages) =>
        XElement.Parse(Xml(options, pages));

    private static IEnumerable<XElement> Urls(XElement urlset) =>
        urlset.Elements(Sitemaps + "url");

    // Joined instead of compared as a sequence, because IsEquivalentTo disregards the order under test.
    private static string Locations(XElement urlset) =>
        string.Join(',', Urls(urlset).Select(url => Value(url, "loc") ?? string.Empty));

    private static string? Value(XElement parent, string name) =>
        parent.Element(Sitemaps + name)?.Value;

    #endregion
}
