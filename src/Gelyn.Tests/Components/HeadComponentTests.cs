using System;
using System.Globalization;
using System.Threading.Tasks;

using Gelyn.Components;
using Gelyn.Core.Context;
using Gelyn.Model;
using Gelyn.Model.Options;

namespace Gelyn.Tests.Components;

/// <summary>
///     Unit tests for the <see cref="HeadComponent"/> class.
/// </summary>
public class HeadComponentTests
{
    #region Author Tests

    /// <summary>
    ///     Verifies that a configured author is stated as metadata, escaped for the attribute it sits in.
    /// </summary>
    [Test]
    public async Task Render_Test_Author_IsStatedWhenConfigured()
    {
        // Arrange
        var options = new SiteOptions { Author = "Ann & Bob" };

        // Act
        string head = Render(options);

        // Assert
        await Assert.That(head).Contains("""<meta name="author" content="Ann &amp; Bob">""");
    }

    /// <summary>
    ///     Verifies that an author nobody configured is left out and never stated as an empty value.
    /// </summary>
    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task Render_Test_BlankAuthor_IsOmitted(string? author)
    {
        // Arrange
        var options = new SiteOptions { Author = author };

        // Act
        string head = Render(options);

        // Assert
        await Assert.That(head).DoesNotContain("name=\"author\"");
    }

    #endregion // Author Tests

    #region Description Tests

    /// <summary>
    ///     Verifies that a page describing itself overrides the description the site carries, so that one
    ///     page can state what it is about on a site that states something broader.
    /// </summary>
    [Test]
    public async Task Render_Test_PageDescription_OverridesTheSiteDescription()
    {
        // Arrange
        var options = new SiteOptions { Description = "Notes about software" };

        // Act
        string head = Render(options, pageDescription: "About the author");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(head).Contains("""<meta name="description" content="About the author">""");
            await Assert.That(head).DoesNotContain("Notes about software");
        }
    }

    /// <summary>
    ///     Verifies that a page describing nothing falls back to the description the site carries.
    /// </summary>
    [Test]
    public async Task Render_Test_AbsentPageDescription_FallsBackToTheSite()
    {
        // Arrange
        var options = new SiteOptions { Description = "Notes about software" };

        // Act
        string head = Render(options);

        // Assert
        await Assert.That(head).Contains("""<meta name="description" content="Notes about software">""");
    }

    /// <summary>
    ///     Verifies that a description neither the page nor the site carries is left out altogether.
    /// </summary>
    [Test]
    public async Task Render_Test_AbsentDescription_IsOmitted()
    {
        // Act
        string head = Render(new SiteOptions());

        // Assert
        await Assert.That(head).DoesNotContain("name=\"description\"");
    }

    #endregion // Description Tests

    #region Favicon Tests

    /// <summary>
    ///     Verifies that the icon resolved for a page is linked explicitly, which is what reaches an icon a
    ///     browser would not request of its own accord.
    /// </summary>
    [Test]
    public async Task Render_Test_Favicon_IsLinkedWhenResolved()
    {
        // Act
        string head = Render(new SiteOptions(), favicon: "/blog/favicon.ico");

        // Assert
        await Assert.That(head).Contains("""<link rel="icon" href="/blog/favicon.ico">""");
    }

    /// <summary>
    ///     Verifies that an icon path is escaped for the attribute it sits in.
    /// </summary>
    [Test]
    public async Task Render_Test_Favicon_IsEscaped()
    {
        // Act
        string head = Render(new SiteOptions(), favicon: "/a&b/favicon.ico");

        // Assert
        await Assert.That(head).Contains("""<link rel="icon" href="/a&amp;b/favicon.ico">""");
    }

    /// <summary>
    ///     Verifies that a site carrying no icon emits no link, not even an empty one.
    /// </summary>
    [Test]
    public async Task Render_Test_AbsentFavicon_IsOmitted()
    {
        // Act
        string head = Render(new SiteOptions());

        // Assert
        await Assert.That(head).DoesNotContain("rel=\"icon\"");
    }

    #endregion // Favicon Tests

    #region Revision Tests

    /// <summary>
    ///     Verifies that a page declaring a revision states that date and not the one it was published on.
    /// </summary>
    [Test]
    public async Task Render_Test_Revision_IsStatedOverThePublicationDate()
    {
        // Act
        string head = Render(new SiteOptions(), date: "2026-09-14", revised: "2026-10-08");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(head).Contains("""<meta name="revised" content="2026-10-08">""");
            await Assert.That(head).DoesNotContain("2026-09-14");
        }
    }

    /// <summary>
    ///     Verifies that a page declaring no revision states its publication date, a page published and never
    ///     revised since having been last revised on the day it went out.
    /// </summary>
    [Test]
    public async Task Render_Test_AbsentRevision_FallsBackToThePublicationDate()
    {
        // Act
        string head = Render(new SiteOptions(), date: "2026-09-14");

        // Assert
        await Assert.That(head).Contains("""<meta name="revised" content="2026-09-14">""");
    }

    /// <summary>
    ///     Verifies that a page declaring neither date states no revision at all, never an empty value.
    /// </summary>
    [Test]
    public async Task Render_Test_UndatedPage_StatesNoRevision()
    {
        // Act
        string head = Render(new SiteOptions());

        // Assert
        await Assert.That(head).DoesNotContain("""name="revised""");
    }

    #endregion // Revision Tests

    #region Feed Tests

    /// <summary>
    ///     Verifies that a site publishing a feed points every page at it, which is how a reader and a
    ///     browser extension find the file.
    /// </summary>
    [Test]
    public async Task Render_Test_Feed_IsAnnouncedWhenPublished()
    {
        // Arrange
        var options = new SiteOptions
        {
            Title = "Gelyn",
            BaseUrl = new Uri("https://example.com", UriKind.Absolute),
            EnableFeed = true,
        };

        // Act
        string head = Render(options);

        // Assert
        await Assert.That(head).Contains(
            """<link rel="alternate" type="application/atom+xml" href="https://example.com/feed.xml" title="Gelyn">""");
    }

    /// <summary>
    ///     Verifies that the link addresses the feed below the path the base URL names, a site served from a
    ///     subdirectory publishing its feed there and not at the root of the domain.
    /// </summary>
    /// <remarks>
    ///     The regression this guards is the one the renderer documents: <c>feed.xml</c> is a relative
    ///     reference, so resolving it against a base URL naming no directory drops that URL's last segment.
    /// </remarks>
    [Test]
    public async Task Render_Test_Feed_KeepsThePathOfTheBaseUrl()
    {
        // Arrange
        var options = new SiteOptions
        {
            BaseUrl = new Uri("https://example.com/gelyn/docs", UriKind.Absolute),
            EnableFeed = true,
        };

        // Act
        string head = Render(options);

        // Assert
        await Assert.That(head)
            .Contains("https://example.com/gelyn/docs/feed.xml")
            .And.DoesNotContain("https://example.com/feed.xml");
    }

    /// <summary>
    ///     Verifies that no page announces a feed unless both switches are on, the file being written only
    ///     when both are, and that a page announces none by default.
    /// </summary>
    [Test]
    [Arguments(null, true)]
    [Arguments("https://example.com", false)]
    [Arguments(null, false)]
    public async Task Render_Test_Feed_IsAnnouncedByNeitherSwitchAlone(string? baseUrl, bool enableFeed)
    {
        // Arrange
        var options = new SiteOptions
        {
            BaseUrl = baseUrl is null ? null : new Uri(baseUrl, UriKind.Absolute),
            EnableFeed = enableFeed,
        };

        // Act
        string head = Render(options);

        // Assert
        await Assert.That(head).DoesNotContain("""rel="alternate""");
    }

    /// <summary>
    ///     Verifies that a site title is escaped for the attribute it is written into.
    /// </summary>
    [Test]
    public async Task Render_Test_Feed_EscapesTheSiteTitle()
    {
        // Arrange
        var options = new SiteOptions
        {
            Title = "Ann & Bob",
            BaseUrl = new Uri("https://example.com", UriKind.Absolute),
            EnableFeed = true,
        };

        // Act
        string head = Render(options);

        // Assert
        await Assert.That(head).Contains("""title="Ann &amp; Bob">""");
    }

    #endregion // Feed Tests

    #region Helpers

    private static DateOnly? Parse(string? date) =>
        date is null ? null : DateOnly.Parse(date, CultureInfo.InvariantCulture);

    private static string Render(
        SiteOptions options,
        string? pageDescription = null,
        string? favicon = null,
        string? date = null,
        string? revised = null) =>
        HeadComponent.Render(new RenderContext
        {
            Options = options,
            Navigation = [],
            Documents = [],
            Page = new ContentPage
            {
                SourcePath = "index.md",
                OutputPath = "index.html",
                Href = "/index.html",
                Title = "Home",
                Description = pageDescription,
                Date = Parse(date),

                // Resolved the way Mapper resolves it, so a case declaring no revision carries the fallback
                // the component is handed in a build.
                Revised = Parse(revised) ?? Parse(date),
                EnableMathRendering = false,
                EnableDiagramRendering = false,
                Html = "<p>Home</p>",
                InNavigation = true,
                InFeed = false,
                Stylesheets = [],
                Scripts = [],
                Favicon = favicon,
            },
        });

    #endregion
}
