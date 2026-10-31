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
