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
///     Unit tests for the <see cref="AtomFeed"/> class.
/// </summary>
public class AtomFeedTests
{
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";

    #region Gate Tests

    /// <summary>
    ///     Verifies that no document is produced while the configuration declares no base URL, every link in
    ///     a feed being absolute and only the base URL supplying an origin to resolve one against.
    /// </summary>
    [Test]
    public async Task Render_Test_AbsentBaseUrl_WritesNothing()
    {
        // Arrange
        var options = new SiteOptions();

        // Act
        byte[] feed = AtomFeed.Render(options, [Page("post.html", "Post", "2026-01-01", inFeed: true)]);

        // Assert
        await Assert.That(feed).IsEmpty();
    }

    /// <summary>
    ///     Verifies that no document is produced when no page qualifies, a feed with no entry having nothing
    ///     to put in the date it is required to carry.
    /// </summary>
    [Test]
    public async Task Render_Test_NoQualifyingPage_WritesNothing()
    {
        // Act
        byte[] feed = AtomFeed.Render(Options(), [Page("post.html", "Post", "2026-01-01")]);

        // Assert
        await Assert.That(feed).IsEmpty();
    }

    #endregion // Gate Tests

    #region Membership Tests

    /// <summary>
    ///     Verifies that a page is an entry only when it opts in, and only when it declares a date.
    /// </summary>
    /// <remarks>
    ///     The dateless page declares the opt-in, which is the conflict only the opt-in model can express:
    ///     RFC 4287 requires an instant of every entry, so the page cannot become one however it asks.
    /// </remarks>
    [Test]
    public async Task Render_Test_Entries_RequireBothTheOptInAndADate()
    {
        // Arrange
        ContentPage[] pages =
        [
            Page("in.html", "In", "2026-01-01", inFeed: true),
            Page("out.html", "Out", "2026-01-02"),
            Page("dateless.html", "Dateless", date: null, inFeed: true),
        ];

        // Act
        string titles = Titles(Render(Options(), pages));

        // Assert
        await Assert.That(titles).IsEqualTo("In");
    }

    /// <summary>
    ///     Verifies that entries run newest first, and that two entries sharing a date are ordered by title.
    /// </summary>
    [Test]
    public async Task Render_Test_Entries_AreOrderedNewestFirst()
    {
        // Arrange
        ContentPage[] pages =
        [
            Page("beta.html", "Beta", "2026-01-01", inFeed: true),
            Page("newest.html", "Newest", "2026-03-01", inFeed: true),
            Page("alpha.html", "Alpha", "2026-01-01", inFeed: true),
        ];

        // Act
        string titles = Titles(Render(Options(), pages));

        // Assert
        await Assert.That(titles).IsEqualTo("Newest,Alpha,Beta");
    }

    #endregion // Membership Tests

    #region Document Tests

    /// <summary>
    ///     Verifies that the elements RFC 4287 requires of a feed are all present.
    /// </summary>
    [Test]
    public async Task Render_Test_RequiredFeedElements_ArePresent()
    {
        // Act
        XElement feed = Render(Options(), [Page("post.html", "Post", "2026-01-01", inFeed: true)]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(feed.Name).IsEqualTo(Atom + "feed");
            await Assert.That(Value(feed, "id")).IsEqualTo("https://example.com/blog/");
            await Assert.That(Value(feed, "title")).IsEqualTo("Gelyn");
            await Assert.That(Value(feed, "updated")).IsEqualTo("2026-01-01T00:00:00Z");
            await Assert.That(feed.Element(Atom + "author")?.Element(Atom + "name")?.Value).IsEqualTo("Author");
        }
    }

    /// <summary>
    ///     Verifies that the elements RFC 4287 requires of an entry are all present, the alternate link among
    ///     them, which an entry carrying no content has to declare.
    /// </summary>
    [Test]
    public async Task Render_Test_RequiredEntryElements_ArePresent()
    {
        // Act
        XElement entry = Entries(Render(Options(), [Page("post.html", "Post", "2026-01-01", inFeed: true)])).Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(Value(entry, "id")).IsEqualTo("https://example.com/blog/post.html");
            await Assert.That(Value(entry, "title")).IsEqualTo("Post");
            await Assert.That(Value(entry, "updated")).IsEqualTo("2026-01-01T00:00:00Z");
            await Assert.That(Value(entry, "published")).IsEqualTo("2026-01-01T00:00:00Z");
            await Assert.That(Link(entry, "alternate")).IsEqualTo("https://example.com/blog/post.html");
        }
    }

    /// <summary>
    ///     Verifies that the feed links to itself and to the site, which is what lets a reader that was handed
    ///     the document alone find both again.
    /// </summary>
    [Test]
    public async Task Render_Test_FeedLinks_AddressTheFeedAndTheSite()
    {
        // Act
        XElement feed = Render(Options(), [Page("post.html", "Post", "2026-01-01", inFeed: true)]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(Link(feed, "self")).IsEqualTo("https://example.com/blog/feed.xml");
            await Assert.That(Link(feed, "alternate")).IsEqualTo("https://example.com/blog/");
        }
    }

    /// <summary>
    ///     Verifies that a base URL addressing the root of a domain is handled as one, so that the feed does
    ///     not address a directory the site has no such thing as.
    /// </summary>
    [Test]
    [Arguments("https://example.com")]
    [Arguments("https://example.com/")]
    public async Task Render_Test_DomainRootBaseUrl_AddressesTheRoot(string baseUrl)
    {
        // Arrange
        SiteOptions options = Options(baseUrl);

        // Act
        XElement feed = Render(options, [Page("post.html", "Post", "2026-01-01", inFeed: true)]);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(Value(feed, "id")).IsEqualTo("https://example.com/");
            await Assert.That(Link(feed, "self")).IsEqualTo("https://example.com/feed.xml");
        }
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
        string xml = Xml(Options(), [Page("post.html", "Post", "2026-01-01", inFeed: true)]);

        // Assert
        await Assert.That(xml).StartsWith("""<?xml version="1.0" encoding="utf-8"?>""");
    }

    /// <summary>
    ///     Verifies that a value carrying markup is escaped, and reads back as the text that went in.
    /// </summary>
    [Test]
    public async Task Render_Test_MarkupInAValue_IsEscaped()
    {
        // Arrange
        ContentPage page = Page("post.html", "Tom & <Jerry>", "2026-01-01", inFeed: true);

        // Act
        string xml = Xml(Options(), [page]);
        XElement entry = Entries(XElement.Parse(xml)).Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(xml).Contains("Tom &amp; &lt;Jerry&gt;");
            await Assert.That(Value(entry, "title")).IsEqualTo("Tom & <Jerry>");
        }
    }

    #endregion // Document Tests

    #region Metadata Tests

    /// <summary>
    ///     Verifies that a site declaring no author is published as anonymous, RFC 4287 requiring an author of
    ///     a feed whose entries declare none and no page carrying one to declare.
    /// </summary>
    [Test]
    public async Task Render_Test_AbsentAuthor_FallsBackToAnonymous()
    {
        // Arrange
        SiteOptions options = Options();
        options.Author = null;

        // Act
        XElement feed = Render(options, [Page("post.html", "Post", "2026-01-01", inFeed: true)]);

        // Assert
        await Assert.That(feed.Element(Atom + "author")?.Element(Atom + "name")?.Value).IsEqualTo("anonymous");
    }

    /// <summary>
    ///     Verifies that an entry describes itself from the site description, never from the description its
    ///     own page declares, and carries no summary when the site declares none.
    /// </summary>
    /// <remarks>
    ///     So every entry of one feed carries the same summary. The page description is deliberately left
    ///     out, which is what makes a per-entry summary a later change and not a fix.
    /// </remarks>
    [Test]
    [Arguments("Site said it", "Site said it")]
    [Arguments(null, null)]
    [Arguments("", null)]
    [Arguments("   ", null)]
    public async Task Render_Test_Summary_ComesFromTheSiteDescription(string? siteDescription, string? expected)
    {
        // Arrange
        SiteOptions options = Options();
        options.Description = siteDescription;

        ContentPage page = Page("post.html", "Post", "2026-01-01", inFeed: true) with
        {
            Description = "Page said it",
        };

        // Act
        XElement entry = Entries(Render(options, [page])).Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(Value(entry, "summary")).IsEqualTo(expected);
            await Assert.That(Xml(options, [page])).DoesNotContain("Page said it");
        }
    }

    /// <summary>
    ///     Verifies that an entry is published on the date its page declares and updated on the date its page
    ///     was revised, which are the two distinct instants Atom asks for.
    /// </summary>
    [Test]
    public async Task Render_Test_Entry_SeparatesTheRevisionFromThePublication()
    {
        // Arrange
        ContentPage page = Page("post.html", "Post", "2026-01-01", inFeed: true, revised: "2026-03-04");

        // Act
        XElement entry = Entries(Render(Options(), [page])).Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(Value(entry, "published")).IsEqualTo("2026-01-01T00:00:00Z");
            await Assert.That(Value(entry, "updated")).IsEqualTo("2026-03-04T00:00:00Z");
        }
    }

    /// <summary>
    ///     Verifies that an entry whose page declares no revision carries one instant in both elements.
    /// </summary>
    [Test]
    public async Task Render_Test_Entry_RepeatsThePublicationWithoutARevision()
    {
        // Act
        XElement entry = Entries(Render(Options(), [Page("post.html", "Post", "2026-01-01", inFeed: true)])).Single();

        // Assert
        await Assert.That(Value(entry, "updated")).IsEqualTo(Value(entry, "published"));
    }

    /// <summary>
    ///     Verifies that the feed is dated from the most recent revision among its entries and not from the
    ///     entry it lists first, which is the most recently published one.
    /// </summary>
    /// <remarks>
    ///     The two differ as soon as an older post is revised: the entry order is unchanged, because a reader
    ///     lists by publication, while the feed itself has changed and has to say so.
    /// </remarks>
    [Test]
    public async Task Render_Test_Feed_IsDatedFromTheNewestRevision()
    {
        // Arrange
        ContentPage[] pages =
        [
            Page("new.html", "New", "2026-05-01", inFeed: true),
            Page("old.html", "Old", "2026-01-01", inFeed: true, revised: "2026-09-09"),
        ];

        // Act
        XElement feed = Render(Options(), pages);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(Value(feed, "updated")).IsEqualTo("2026-09-09T00:00:00Z");
            await Assert.That(Titles(feed)).IsEqualTo("New,Old");
        }
    }

    #endregion // Metadata Tests

    #region Determinism Tests

    /// <summary>
    ///     Verifies that the same pages render to the same bytes, so that a build of unchanged content leaves
    ///     the feed as the build before it wrote it.
    /// </summary>
    /// <remarks>
    ///     This is what dating the feed from its newest entry buys, where a build timestamp would have made
    ///     every build differ and cost the output the determinism the rest of it has.
    /// </remarks>
    [Test]
    public async Task Render_Test_SameInput_ProducesIdenticalBytes()
    {
        // Arrange
        ContentPage[] pages = [Page("post.html", "Post", "2026-01-01", inFeed: true)];

        // Act and Assert
        // Compared as base64, which is sensitive to the order of the bytes where IsEquivalentTo is not.
        string first = Convert.ToBase64String(AtomFeed.Render(Options(), pages));
        string second = Convert.ToBase64String(AtomFeed.Render(Options(), pages));

        await Assert.That(first).IsEqualTo(second);
    }

    #endregion // Determinism Tests

    #region Helpers

    private static SiteOptions Options(string baseUrl = "https://example.com/blog") =>
        new()
        {
            Title = "Gelyn",
            Author = "Author",
            Description = null,
            BaseUrl = new Uri(baseUrl, UriKind.Absolute),
        };

    private static ContentPage Page(
        string outputPath,
        string title,
        string? date,
        bool inFeed = false,
        string? revised = null) =>
        new()
        {
            SourcePath = Path.ChangeExtension(outputPath, ".md"),
            OutputPath = outputPath,
            Href = $"/blog/{outputPath}",
            Title = title,
            Description = null,
            Date = Parse(date),

            // Resolved the way Mapper resolves it, so that a case declaring no revision carries the fallback
            // the renderer is handed in a build and not a null the renderer never sees.
            Revised = Parse(revised) ?? Parse(date),
            EnableMathRendering = false,
            EnableDiagramRendering = false,
            Html = string.Empty,
            InNavigation = false,
            InFeed = inFeed,
            Stylesheets = [],
            Scripts = [],
            Favicon = null,
        };

    private static DateOnly? Parse(string? date) =>
        date is null ? null : DateOnly.Parse(date, CultureInfo.InvariantCulture);

    private static string Xml(SiteOptions options, IReadOnlyList<ContentPage> pages) =>
        Encoding.UTF8.GetString(AtomFeed.Render(options, pages));

    private static XElement Render(SiteOptions options, IReadOnlyList<ContentPage> pages) =>
        XElement.Parse(Xml(options, pages));

    private static IEnumerable<XElement> Entries(XElement feed) =>
        feed.Elements(Atom + "entry");

    // Joined instead of compared as a sequence, because IsEquivalentTo disregards the order under test.
    private static string Titles(XElement feed) =>
        string.Join(',', Entries(feed).Select(entry => entry.Element(Atom + "title")?.Value ?? string.Empty));

    private static string? Value(XElement parent, string name) =>
        parent.Element(Atom + name)?.Value;

    // Matched on the relation, because a feed and an entry both carry more than one link.
    private static string? Link(XElement parent, string relation) =>
        parent.Elements(Atom + "link").FirstOrDefault(link => Relation(link) == relation)?.Attribute("href")?.Value;

    private static string? Relation(XElement link) =>
        link.Attribute("rel")?.Value;

    #endregion
}
