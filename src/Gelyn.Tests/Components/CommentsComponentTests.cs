using System.Threading.Tasks;

using Gelyn.Components;
using Gelyn.Core;
using Gelyn.Core.Context;
using Gelyn.Model;
using Gelyn.Model.Options;

namespace Gelyn.Tests.Components;

/// <summary>
///     Unit tests for the <see cref="CommentsComponent"/> class.
/// </summary>
public class CommentsComponentTests
{
    #region Render Tests

    /// <summary>
    ///     Verifies that a page enabling comments on a site configuring a backend loads the giscus client with
    ///     each of the four backend values.
    /// </summary>
    [Test]
    public async Task Render_Test_ConfiguredSiteAndOptedInPage_RendersTheWidget()
    {
        // Act
        string html = Render(Giscus(), enableComments: true);

        // Assert
        await Assert.That(html)
            .Contains("""<section class="giscus" aria-label="Comments">""")
            .And.Contains("<script src=\"https://giscus.app/client.js\"")
            .And.Contains("data-repo=\"octocat/blog\"")
            .And.Contains("data-repo-id=\"R_kgDOxxxxxxx\"")
            .And.Contains("data-category=\"Comments\"")
            .And.Contains("data-category-id=\"DIC_kwDOxxxxxxxxxxxx\"");
    }

    /// <summary>
    ///     Verifies that the presentation attributes are fixed, and that no language is passed, because giscus
    ///     accepts only its own codes and an unknown one addresses a widget page that does not exist.
    /// </summary>
    [Test]
    public async Task Render_Test_PresentationAttributes_AreFixed()
    {
        // Act
        string html = Render(Giscus(), enableComments: true);

        // Assert
        await Assert.That(html)
            .Contains("data-mapping=\"pathname\"")
            .And.Contains("data-strict=\"1\"")
            .And.Contains("data-theme=\"preferred_color_scheme\"")
            .And.Contains("data-loading=\"lazy\"")
            .And.Contains("crossorigin=\"anonymous\"")
            .And.Contains(" async")
            .And.DoesNotContain("data-lang");
    }

    /// <summary>
    ///     Verifies that neither a configured backend nor a page enabling comments renders the widget alone.
    /// </summary>
    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    public async Task Render_Test_Widget_IsRenderedByNeitherGateAlone(bool configured, bool enabled)
    {
        // Act
        string html = Render(configured ? Giscus() : null, enableComments: enabled);

        // Assert
        await Assert.That(html).IsEmpty();
    }

    /// <summary>
    ///     Verifies that the not-found page renders no widget, whatever its front matter declares, because it
    ///     is served at whichever address missed and would open one thread per miss.
    /// </summary>
    [Test]
    public async Task Render_Test_NotFoundPage_RendersNoWidget()
    {
        // Act
        string html = Render(Giscus(), enableComments: true, isNotFound: true);

        // Assert
        await Assert.That(html).IsEmpty();
    }

    /// <summary>
    ///     Verifies that a configured value is escaped before it reaches an attribute.
    /// </summary>
    [Test]
    public async Task Render_Test_Values_AreEscaped()
    {
        // Arrange
        GiscusOptions giscus = Giscus();
        giscus.Category = "Q&A";

        // Act
        string html = Render(giscus, enableComments: true);

        // Assert
        await Assert.That(html).Contains("data-category=\"Q&amp;A\"");
    }

    #endregion // Render Tests

    #region Helpers

    private static GiscusOptions Giscus() => new()
    {
        Repo = "octocat/blog",
        RepoId = "R_kgDOxxxxxxx",
        Category = "Comments",
        CategoryId = "DIC_kwDOxxxxxxxxxxxx",
    };

    private static string Render(GiscusOptions? giscus, bool enableComments, bool isNotFound = false)
    {
        var page = new ContentPage
        {
            SourcePath = "index.md",
            OutputPath = "index.html",
            Href = "/index.html",
            Title = "Home",
            Description = null,
            Date = null,
            Revised = null,
            EnableMathRendering = false,
            EnableDiagramRendering = false,
            EnableSyntaxHighlighting = false,
            EnableComments = enableComments,
            Html = "<p>Home</p>",
            InNavigation = true,
            IsNotFoundPage = isNotFound,
            InFeed = false,
            Stylesheets = [],
            Scripts = [],
            Favicon = null,
        };

        return CommentsComponent.Render(new RenderContext
        {
            Options = new SiteOptions { Giscus = giscus },
            Navigation = [],
            Documents = new DocumentCatalog([page]),
            Page = page,
        });
    }

    #endregion
}
