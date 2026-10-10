using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Gelyn.Model;
using Gelyn.Model.Options;
using Gelyn.Services;
using Gelyn.Tests.Fixtures;
using Gelyn.Tests.Stubs;

namespace Gelyn.Tests.Services;

/// <summary>
///     Unit tests for the <see cref="ContentPipeline"/> class.
/// </summary>
public class ContentPipelineTests
{
    #region Scan Tests

    /// <summary>
    ///     Verifies that the landing page is the first result, which callers rely on instead of sorting.
    /// </summary>
    [Test]
    public async Task Scan_Test_LandingPage_IsTheFirstResult()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.Create();

        // Act
        IReadOnlyList<ContentItem> items = Scan(fixture);

        // Assert
        await Assert.That(items[0].Href).IsEqualTo("/index.html");
    }

    /// <summary>
    ///     Verifies that only the landing page, root-level pages and the index of a top-level section are
    ///     marked for the navigation.
    /// </summary>
    [Test]
    public async Task Scan_Test_Navigation_CoversTheFirstTwoLevelsOnly()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.Create();
        string[] expected = ["/index.html", "/about.html", "/blog/index.html"];

        // Act
        IReadOnlyList<ContentItem> items = Scan(fixture);

        // Assert
        IEnumerable<string> linked = items
            .Where(item => item.InNavigation)
            .Select(item => item.Href);

        await Assert.That(linked).IsEquivalentTo(expected);
    }

    /// <summary>
    ///     Verifies that a directory symbolic link is not descended into. A link resolving to an ancestor
    ///     makes the walk unbounded, and any link at all lets it leave the content directory.
    /// </summary>
    /// <remarks>
    ///     Driven against the real file system, because <c>MockFileSystem</c> does not resolve traversal
    ///     through a link: it yields nothing on the far side whether the guard is present or not, so a
    ///     mocked test cannot tell a guarded walk from an unguarded one.
    /// </remarks>
    [Test]
    public async Task Scan_Test_DirectorySymlink_IsNotFollowed()
    {
        // Arrange
        Skip.Unless(!OperatingSystem.IsWindows(), "Creating a directory symlink needs elevation on Windows.");

        FileSystem fileSystem = new();
        SiteOptions options = new();
        string root = Path.Combine(Path.GetTempPath(), $"gelyn-{Guid.NewGuid():N}");
        string content = Path.Combine(root, options.ContentDirectory);

        try
        {
            Directory.CreateDirectory(Path.Combine(content, "posts"));
            await File.WriteAllTextAsync(Path.Combine(content, "index.md"), "---\ntitle: Home\n---");
            await File.WriteAllTextAsync(Path.Combine(content, "posts", "note.md"), "# Note");
            Directory.CreateSymbolicLink(Path.Combine(content, "loop"), content);

            HostEnvironmentStub environment = new() { ContentRootPath = root };
            ContentPipeline pipeline = new(environment, fileSystem);

            // Act
            IReadOnlyList<ContentItem> items = pipeline.Scan(options);

            // Assert
            await Assert.That(items.Select(item => item.Href))
                .IsEquivalentTo(new[] { "/index.html", "/posts/note.html" });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    ///     Verifies that a content directory without a landing page is rejected.
    /// </summary>
    [Test]
    public async Task Scan_Test_MissingLandingPage_Throws()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.CreateEmpty().Write("about.md", "About");

        // Act & Assert
        await Assert.That(() => Scan(fixture)).Throws<FileNotFoundException>();
    }

    /// <summary>
    ///     Verifies that files which are not Markdown are left out of the scan.
    /// </summary>
    [Test]
    public async Task Scan_Test_NonMarkdownFiles_AreIgnored()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.CreateEmpty()
            .Write("index.md", "Home")
            .Write("style.css")
            .Write("blog/photo.png");

        // Act
        IReadOnlyList<ContentItem> items = Scan(fixture);

        // Assert
        await Assert.That(items.Select(item => item.Href)).IsEquivalentTo(new[] { "/index.html" });
    }

    /// <summary>
    ///     Verifies that the path of the base URL is prefixed to every link however deep the page sits, and
    ///     that a trailing slash on it does not double up against the slash the link already starts with.
    /// </summary>
    [Test]
    [Arguments("https://example.com/gelyn/docs")]
    [Arguments("https://example.com/gelyn/docs/")]
    public async Task Scan_Test_BaseUrlPath_IsPrefixedToEveryHref(string baseUrl)
    {
        // Arrange
        ContentFixture fixture = ContentFixture.Create().WithBaseUrl(baseUrl);

        // Act
        IReadOnlyList<ContentItem> items = Scan(fixture);
        ContentItem deep = items.Single(item => item.OutputPath.EndsWith("deep.html", StringComparison.Ordinal));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(items[0].Href).IsEqualTo("/gelyn/docs/index.html");
            await Assert.That(deep.Href).IsEqualTo("/gelyn/docs/blog/2026/q3/deep.html");
        }
    }

    /// <summary>
    ///     Verifies that a base URL addressing the root of a domain leaves every link exactly as it would be
    ///     without one, so that configuring one for the canonical reference alone costs nothing.
    /// </summary>
    [Test]
    [Arguments("https://example.com")]
    [Arguments("https://example.com/")]
    public async Task Scan_Test_BaseUrlAtDomainRoot_LeavesHrefsUnprefixed(string baseUrl)
    {
        // Arrange
        ContentFixture fixture = ContentFixture.Create().WithBaseUrl(baseUrl);

        // Act
        IReadOnlyList<ContentItem> items = Scan(fixture);

        // Assert
        await Assert.That(items[0].Href).IsEqualTo("/index.html");
    }

    #endregion // Scan Tests

    #region RenderAsync Tests

    /// <summary>
    ///     Verifies that Markdown nested below a section is rendered however deep it sits, and that the
    ///     output path uses the native separator while the URL uses forward slashes.
    /// </summary>
    [Test]
    public async Task RenderAsync_Test_NestedContent_IsRendered()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.Create();

        // Act
        IReadOnlyList<ContentPage> pages = await RenderAsync(fixture);
        ContentPage deep = pages.Single(page => page.Title == "Deep");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(deep.Href).IsEqualTo("/blog/2026/q3/deep.html");
            await Assert.That(deep.OutputPath).IsEqualTo(Path.Combine("blog", "2026", "q3", "deep.html"));
        }
    }

    /// <summary>
    ///     Verifies that a page declaring no title falls back to its file name, or to the folder name when it
    ///     is a section index.
    /// </summary>
    /// <remarks>
    ///     The landing page is the case with no folder to fall back to, so it resolves to the file name like
    ///     any other root-level page rather than to the name of the content directory.
    /// </remarks>
    [Test]
    public async Task RenderAsync_Test_MissingFrontMatterTitle_FallsBackToTheName()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.CreateEmpty()
            .Write("index.md")
            .Write("colophon.md")
            .Write("blog/index.md");

        // Act
        IReadOnlyList<ContentPage> pages = await RenderAsync(fixture);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(pages.Single(page => page.Href == "/index.html").Title).IsEqualTo("index");
            await Assert.That(pages.Single(page => page.Href == "/colophon.html").Title).IsEqualTo("colophon");
            await Assert.That(pages.Single(page => page.Href == "/blog/index.html").Title).IsEqualTo("blog");
        }
    }

    #endregion // RenderAsync Tests

    #region Helpers

    private static IReadOnlyList<ContentItem> Scan(ContentFixture fixture) =>
        fixture.Pipeline.Scan(fixture.Options);

    private static Task<IReadOnlyList<ContentPage>> RenderAsync(ContentFixture fixture) =>
        fixture.Pipeline.RenderAsync(Scan(fixture), CancellationToken.None);

    #endregion
}
