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
    ///     Verifies that files which are not Markdown are located for copying rather than rendered, keeping
    ///     the name they were authored under instead of being renamed to an HTML extension.
    /// </summary>
    [Test]
    public async Task Scan_Test_UnsupportedFiles_AreCopiedNotRendered()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.CreateEmpty()
            .Write("index.md", "Home")
            .WriteFile("style.css")
            .WriteBytes("blog/photo.png", [0x89, 0x50, 0x4E, 0x47]);

        // Act
        IReadOnlyList<ContentItem> items = Scan(fixture);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(items.Where(item => item.Kind is ContentKind.Page).Select(item => item.Href))
                .IsEquivalentTo(new[] { "/index.html" });

            await Assert.That(items.Where(item => item.Kind is ContentKind.MiscFile).Select(item => item.Href))
                .IsEquivalentTo(new[] { "/style.css", "/blog/photo.png" });
        }
    }

    /// <summary>
    ///     Verifies that a file the build has no special treatment for is still located, so that a site can
    ///     publish the files a host expects to find beside its pages.
    /// </summary>
    [Test]
    public async Task Scan_Test_MiscFile_IsLocatedAtTheMirroredPath()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.CreateEmpty()
            .Write("index.md", "Home")
            .WriteFile("robots.txt", "User-agent: *");

        // Act
        ContentItem robots = Scan(fixture).Single(item => item.Kind is ContentKind.MiscFile);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(robots.OutputPath).IsEqualTo("robots.txt");
            await Assert.That(robots.Href).IsEqualTo("/robots.txt");
            await Assert.That(robots.InNavigation).IsFalse();
        }
    }

    /// <summary>
    ///     Verifies that the files the build has no use for are left out, which is what keeps editor and tool
    ///     droppings from being published alongside the site.
    /// </summary>
    [Test]
    public async Task Scan_Test_DeniedFiles_AreLeftOut()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.CreateEmpty()
            .Write("index.md", "Home")
            .WriteFile(".DS_Store")
            .WriteFile("notes.md~")
            .WriteFile("obj/build.log");

        // Act
        IReadOnlyList<ContentItem> items = Scan(fixture);

        // Assert
        await Assert.That(items.Select(item => item.Href)).IsEquivalentTo(new[] { "/index.html" });
    }

    /// <summary>
    ///     Verifies that HTML in the content tree is left out, since the page rendered from the Markdown
    ///     beside it claims the same output path and the copy would overwrite it.
    /// </summary>
    [Test]
    public async Task Scan_Test_Html_IsLeftOut()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.CreateEmpty()
            .Write("index.md", "Home")
            .Write("about.md", "About")
            .WriteFile("about.html", "<p>stale</p>");

        // Act
        IReadOnlyList<ContentItem> items = Scan(fixture);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(items.Select(item => item.Href))
                .IsEquivalentTo(new[] { "/index.html", "/about.html" });

            await Assert.That(items.Single(item => item.Href == "/about.html").Kind)
                .IsEqualTo(ContentKind.Page);
        }
    }

    /// <summary>
    ///     Verifies that a root-level file which is not a page stays out of the navigation, which the shared
    ///     item type would otherwise mark for it on depth alone.
    /// </summary>
    [Test]
    public async Task Scan_Test_RootLevelMiscFile_IsNotInTheNavigation()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.CreateEmpty()
            .Write("index.md", "Home")
            .WriteBytes("logo.png", [0x89, 0x50, 0x4E, 0x47]);

        // Act
        IReadOnlyList<ContentItem> items = Scan(fixture);

        // Assert
        await Assert.That(items.Single(item => item.InNavigation).Href).IsEqualTo("/index.html");
    }

    /// <summary>
    ///     Verifies that a file symbolic link is skipped, including one pointing at Markdown, so that a copy
    ///     cannot publish the bytes of a target outside the content directory.
    /// </summary>
    /// <remarks>
    ///     Driven against the real file system, because <c>MockFileSystem</c> does not model links.
    /// </remarks>
    [Test]
    public async Task Scan_Test_FileSymlink_IsNotFollowed()
    {
        // Arrange
        Skip.Unless(!OperatingSystem.IsWindows(), "Creating a symbolic link needs elevation on Windows.");

        FileSystem fileSystem = new();
        SiteOptions options = new();
        string root = Path.Combine(Path.GetTempPath(), $"gelyn-{Guid.NewGuid():N}");
        string content = Path.Combine(root, options.ContentDirectory);
        string outside = Path.Combine(root, "secret.md");

        try
        {
            Directory.CreateDirectory(content);
            await File.WriteAllTextAsync(Path.Combine(content, "index.md"), "---\ntitle: Home\n---");
            await File.WriteAllTextAsync(outside, "# Secret");
            File.CreateSymbolicLink(Path.Combine(content, "leak.md"), outside);

            HostEnvironmentStub environment = new() { ContentRootPath = root };
            ContentPipeline pipeline = new(environment, fileSystem);

            // Act
            IReadOnlyList<ContentItem> items = pipeline.Scan(options);

            // Assert
            await Assert.That(items.Select(item => item.Href)).IsEquivalentTo(new[] { "/index.html" });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
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
