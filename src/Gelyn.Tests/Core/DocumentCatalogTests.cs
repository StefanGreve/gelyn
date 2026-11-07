using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Gelyn.Core;
using Gelyn.Model;

namespace Gelyn.Tests.Core;

/// <summary>
///     Unit tests for the <see cref="DocumentCatalog"/> class.
/// </summary>
public class DocumentCatalogTests
{
    #region Feed Tests

    /// <summary>
    ///     Verifies that a dated page declaring the feed makes the site one with feed entries.
    /// </summary>
    [Test]
    public async Task HasFeedEntries_Test_DatedPageInFeed_IsTrue()
    {
        // Arrange
        var catalog = new DocumentCatalog([Page("index.html", "Home"), Page("post.html", "Post", "2026-01-01", inFeed: true)]);

        // Act and Assert
        await Assert.That(catalog.HasFeedEntries).IsTrue();
    }

    /// <summary>
    ///     Verifies that a page declaring the feed without a date is no entry, Atom requiring a date of every
    ///     entry, so the site has none.
    /// </summary>
    [Test]
    public async Task HasFeedEntries_Test_UndatedPageInFeed_IsFalse()
    {
        // Arrange
        var catalog = new DocumentCatalog([Page("index.html", "Home"), Page("post.html", "Post", inFeed: true)]);

        // Act and Assert
        await Assert.That(catalog.HasFeedEntries).IsFalse();
    }

    #endregion // Feed Tests

    #region Listing Tests

    /// <summary>
    ///     Verifies that a nested index is listed twice: as a file beside the pages of its own directory, and
    ///     as the row standing for that directory one level up.
    /// </summary>
    [Test]
    public async Task ListedIn_Test_NestedIndex_StandsForItsDirectoryOneLevelUp()
    {
        // Arrange
        var catalog = new DocumentCatalog(
        [
            Page("index.html", "Home", "2026-01-01"),
            Page("blog/index.html", "Blog", "2026-02-01"),
            Page("blog/hello.html", "Hello", "2026-03-01"),
        ]);

        // Act
        string root = Titles(catalog.ListedIn(string.Empty));
        string blog = Titles(catalog.ListedIn(Native("blog/")));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(root).IsEqualTo("Blog,Home");
            await Assert.That(blog).IsEqualTo("Hello,Blog");
        }
    }

    /// <summary>
    ///     Verifies that a directory no listed page sits in or directly below lists nothing, instead of
    ///     failing the lookup.
    /// </summary>
    [Test]
    public async Task ListedIn_Test_UnknownScope_IsEmpty()
    {
        // Arrange
        var catalog = new DocumentCatalog([Page("index.html", "Home", "2026-01-01")]);

        // Act and Assert
        await Assert.That(catalog.ListedIn(Native("blog/"))).IsEmpty();
    }

    /// <summary>
    ///     Verifies that pages sharing a date and a title keep the order they were found in, which is what
    ///     keeps a build's listings the same from one run to the next.
    /// </summary>
    [Test]
    public async Task ListedIn_Test_PagesSharingDateAndTitle_KeepScanOrder()
    {
        // Arrange
        var catalog = new DocumentCatalog(
        [
            Page("b.html", "Same", "2026-01-01"),
            Page("a.html", "Same", "2026-01-01"),
        ]);

        // Act
        string paths = string.Join(',', catalog.ListedIn(string.Empty).Select(static page => page.OutputPath));

        // Assert
        await Assert.That(paths).IsEqualTo("b.html,a.html");
    }

    #endregion // Listing Tests

    #region Helpers

    private static string Titles(IReadOnlyList<ContentPage> pages) =>
        string.Join(',', pages.Select(static page => page.Title));

    private static string Native(string path) =>
        path.Replace('/', Path.DirectorySeparatorChar);

    private static ContentPage Page(string outputPath, string title, string? date = null, bool inFeed = false) =>
        new()
        {
            SourcePath = Native(Path.ChangeExtension(outputPath, ".md")),
            OutputPath = Native(outputPath),
            Href = $"/{outputPath}",
            Title = title,
            Description = null,
            Date = Parse(date),
            Revised = Parse(date),
            EnableMathRendering = false,
            EnableDiagramRendering = false,
            EnableSyntaxHighlighting = false,
            EnableComments = false,
            Html = string.Empty,
            InNavigation = false,
            IsNotFoundPage = false,
            InFeed = inFeed,
            Stylesheets = [],
            Scripts = [],
            Favicon = null,
            UnknownKeys = [],
        };

    private static DateOnly? Parse(string? date) =>
        date is null ? null : DateOnly.Parse(date, CultureInfo.InvariantCulture);

    #endregion
}
