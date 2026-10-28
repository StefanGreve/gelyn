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
        SiteOptions options = new() { Author = "Ann & Bob" };

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
        SiteOptions options = new() { Author = author };

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
        SiteOptions options = new() { Description = "Notes about software" };

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
        SiteOptions options = new() { Description = "Notes about software" };

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

    #region Helpers

    private static string Render(SiteOptions options, string? pageDescription = null, string? favicon = null) =>
        HeadComponent.Render(new RenderContext
        {
            Options = options,
            Navigation = [],
            Page = new ContentPage
            {
                OutputPath = "index.html",
                Href = "/index.html",
                Title = "Home",
                Description = pageDescription,
                Date = null,
                EnableMathRendering = false,
                EnableDiagramRendering = false,
                Html = "<p>Home</p>",
                InNavigation = true,
                Stylesheets = [],
                Scripts = [],
                Favicon = favicon,
            },
        });

    #endregion
}
