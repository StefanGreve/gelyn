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
///     Unit tests for the <see cref="ContentWalker"/> class.
/// </summary>
public class ContentWalkerTests
{
    #region WalkAsync Tests

    /// <summary>
    ///     Verifies that the landing page is the first result, which callers rely on instead of sorting.
    /// </summary>
    [Test]
    public async Task WalkAsync_Test_LandingPage_IsTheFirstResult()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.Create();

        // Act
        IReadOnlyList<ContentPage> pages = await Walk(fixture);

        // Assert
        await Assert.That(pages[0].Href).IsEqualTo("/index.html");
    }

    /// <summary>
    ///     Verifies that Markdown nested below a section is rendered however deep it sits, and that the
    ///     output path uses the native separator while the URL uses forward slashes.
    /// </summary>
    [Test]
    public async Task WalkAsync_Test_NestedContent_IsRendered()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.Create();

        // Act
        IReadOnlyList<ContentPage> pages = await Walk(fixture);
        ContentPage deep = pages.Single(page => page.Title == "Deep");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(deep.Href).IsEqualTo("/blog/2026/q3/deep.html");
            await Assert.That(deep.OutputPath).IsEqualTo(Path.Combine("blog", "2026", "q3", "deep.html"));
        }
    }

    /// <summary>
    ///     Verifies that only the landing page, root-level pages and the index of a top-level section are
    ///     marked for the navigation.
    /// </summary>
    [Test]
    public async Task WalkAsync_Test_Navigation_CoversTheFirstTwoLevelsOnly()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.Create();
        string[] expected = ["/index.html", "/about.html", "/blog/index.html"];

        // Act
        IReadOnlyList<ContentPage> pages = await Walk(fixture);

        // Assert
        IEnumerable<string> linked = pages
            .Where(page => page.InNavigation)
            .Select(page => page.Href);

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
    public async Task WalkAsync_Test_DirectorySymlink_IsNotFollowed()
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
            ContentWalker walker = new(new MarkdigRenderer(), environment, fileSystem);

            // Act
            IReadOnlyList<ContentPage> pages = await walker.WalkAsync(options, CancellationToken.None);

            // Assert
            await Assert.That(pages.Select(page => page.Href))
                .IsEquivalentTo(new[] { "/index.html", "/posts/note.html" });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    ///     Verifies that a content directory without a landing page is rejected, and that the failure
    ///     surfaces from the call rather than from enumerating the result.
    /// </summary>
    [Test]
    public async Task WalkAsync_Test_MissingLandingPage_Throws()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.CreateEmpty().Write("about.md", "About");

        // Act & Assert
        await Assert.That(async () => await Walk(fixture)).Throws<FileNotFoundException>();
    }

    /// <summary>
    ///     Verifies that files which are not Markdown are left out of the walk.
    /// </summary>
    [Test]
    public async Task WalkAsync_Test_NonMarkdownFiles_AreIgnored()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.CreateEmpty()
            .Write("index.md", "Home")
            .Write("style.css")
            .Write("blog/photo.png");

        // Act
        IReadOnlyList<ContentPage> pages = await Walk(fixture);

        // Assert
        await Assert.That(pages.Select(page => page.Href)).IsEquivalentTo(new[] { "/index.html" });
    }

    /// <summary>
    ///     Verifies that a source declaring no title falls back to its file name, or to the folder name
    ///     when it is a section index.
    /// </summary>
    [Test]
    public async Task WalkAsync_Test_MissingFrontMatterTitle_FallsBackToTheName()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.CreateEmpty()
            .Write("index.md", "Home")
            .Write("colophon.md")
            .Write("blog/index.md");

        // Act
        IReadOnlyList<ContentPage> pages = await Walk(fixture);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(pages.Single(page => page.Href == "/colophon.html").Title).IsEqualTo("colophon");
            await Assert.That(pages.Single(page => page.Href == "/blog/index.html").Title).IsEqualTo("blog");
        }
    }

    #endregion // WalkAsync Tests

    #region Helpers

    private static Task<IReadOnlyList<ContentPage>> Walk(ContentFixture fixture) =>
        fixture.Walker.WalkAsync(fixture.Options, CancellationToken.None);

    #endregion
}
