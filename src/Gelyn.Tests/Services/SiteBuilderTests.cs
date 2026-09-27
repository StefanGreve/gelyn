using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Gelyn.Services;
using Gelyn.Tests.Fixtures;

namespace Gelyn.Tests.Services;

/// <summary>
///     Unit tests for the <see cref="SiteBuilder"/> class.
/// </summary>
public partial class SiteBuilderTests
{
    #region BuildAsync Tests

    /// <summary>
    ///     Verifies that the landing page is written before every other page.
    /// </summary>
    [Test]
    public async Task BuildAsync_Test_LandingPage_IsWrittenFirst()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.Create();

        // Act
        IReadOnlyList<string> written = await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(written.Count).IsEqualTo(6);
            await Assert.That(written[0]).IsEqualTo("index.html");
        }
    }

    /// <summary>
    ///     Verifies that the navigation links the landing page, every root-level page and the index of
    ///     each top-level section, that nothing nested more deeply is linked, and that the landing page
    ///     comes first.
    /// </summary>
    /// <remarks>
    ///     Asserted against the rendered markup because the render context never leaves
    ///     <see cref="SiteBuilder"/>: the layout is a static class, so there is no seam through which the
    ///     navigation could be observed directly. The order matters because the header reads the first
    ///     entry to link the site title.
    /// </remarks>
    [Test]
    public async Task BuildAsync_Test_Navigation_LinksTopLevelPagesOnly()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.Create();
        string[] expected = ["/index.html", "/about.html", "/blog/index.html"];

        // Act
        await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        foreach (string page in fixture.EnumerateOutput())
        {
            string[] navigation = [.. ReadNavigation(fixture, page)];

            using (Assert.Multiple())
            {
                await Assert.That(navigation).IsEquivalentTo(expected);
                await Assert.That(navigation[0]).IsEqualTo("/index.html");
            }
        }
    }

    #endregion // BuildAsync Tests

    #region Helpers

    [GeneratedRegex("<nav>.*?</nav>", RegexOptions.Singleline)]
    private static partial Regex NavigationBlock();

    [GeneratedRegex("href=\"(?<href>[^\"]*)\"")]
    private static partial Regex NavigationLink();

    private static IEnumerable<string> ReadNavigation(ContentFixture fixture, string path)
    {
        string navigation = NavigationBlock().Match(fixture.ReadOutput(path)).Value;

        return NavigationLink()
            .Matches(navigation)
            .Select(match => match.Groups["href"].Value);
    }

    #endregion // Helpers
}
