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
                .IsEquivalentTo(["/index.html", "/posts/note.html"]);
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
                .IsEquivalentTo(["/index.html"]);

            await Assert.That(items.Where(item => item.Kind is not ContentKind.Page).Select(item => item.Href))
                .IsEquivalentTo(["/style.css", "/blog/photo.png"]);
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
    /// <remarks>
    ///     The patterns are configured rather than built in, so a project without them publishes these files.
    ///     <c>gelyn.json</c> in this repository carries the set a project is expected to start from.
    /// </remarks>
    [Test]
    public async Task Scan_Test_DeniedFiles_AreLeftOut()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.CreateEmpty()
            .WithExcludePatterns(".DS_Store", "*~", "obj")
            .Write("index.md", "Home")
            .WriteFile(".DS_Store")
            .WriteFile("notes.md~")
            .WriteFile("obj/build.log");

        // Act
        IReadOnlyList<ContentItem> items = Scan(fixture);

        // Assert
        await Assert.That(items.Select(item => item.Href)).IsEquivalentTo(["/index.html"]);
    }

    /// <summary>
    ///     Verifies that a configured exclude pattern leaves matching files and directories out, so that an
    ///     author can skip something the built-in list does not know about.
    /// </summary>
    [Test]
    public async Task Scan_Test_ConfiguredExcludePatterns_AreHonoured()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.CreateEmpty()
            .WithExcludePatterns("*.xcf", "drafts")
            .Write("index.md", "Home")
            .WriteFile("logo.xcf")
            .Write("drafts/wip.md", "WIP")
            .WriteFile("robots.txt", "User-agent: *");

        // Act
        IReadOnlyList<ContentItem> items = Scan(fixture);

        // Assert
        await Assert.That(items.Select(item => item.Href))
            .IsEquivalentTo(["/index.html", "/robots.txt"]);
    }

    /// <summary>
    ///     Verifies that HTML in the content tree is left out, since the page rendered from the Markdown
    ///     beside it claims the same output path and the copy would overwrite it.
    /// </summary>
    /// <remarks>
    ///     Only while <c>*.html</c> is configured. Without it the copy runs after the render and publishes
    ///     the stale file over the generated page, which is why <c>gelyn.json</c> ships the pattern.
    /// </remarks>
    [Test]
    public async Task Scan_Test_Html_IsLeftOut()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.CreateEmpty()
            .WithExcludePatterns("*.html")
            .Write("index.md", "Home")
            .Write("about.md", "About")
            .WriteFile("about.html", "<p>stale</p>");

        // Act
        IReadOnlyList<ContentItem> items = Scan(fixture);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(items.Select(item => item.Href))
                .IsEquivalentTo(["/index.html", "/about.html"]);

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
    ///     Kept on the real file system beside <see cref="Scan_Test_DirectorySymlink_IsNotFollowed"/>, which has
    ///     no mocked equivalent. <c>MockFileSystem</c> does model a link, so this case alone could be mocked.
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
            await Assert.That(items.Select(item => item.Href)).IsEquivalentTo(["/index.html"]);
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

    /// <summary>
    ///     Verifies that a stylesheet at the root of the content tree applies to every page, because the root
    ///     is every page's ancestor rather than because it is treated as a special case.
    /// </summary>
    [Test]
    public async Task Scan_Test_RootStylesheet_AppliesToEveryPage()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.CreateStyled();

        // Act
        IReadOnlyList<ContentItem> items = Scan(fixture);

        // Assert
        IReadOnlyList<string> outermost = [.. items
            .Where(item => item.Kind is ContentKind.Page)
            .Select(item => item.Stylesheets.FirstOrDefault() ?? string.Empty)];

        using (Assert.Multiple())
        {
            await Assert.That(outermost.Count).IsEqualTo(5);
            await Assert.That(outermost.Distinct()).IsEquivalentTo(["/styles.css"]);
        }
    }

    /// <summary>
    ///     Verifies that a page links the stylesheet of every directory above it as well as its own, ordered
    ///     outermost first so that the deepest one wins the cascade.
    /// </summary>
    [Test]
    public async Task Scan_Test_Stylesheets_CascadeOutermostFirst()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.CreateStyled();

        // Act
        IReadOnlyList<ContentItem> items = Scan(fixture);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(Stylesheets(items, "/index.html")).IsEqualTo("/styles.css");
            await Assert.That(Stylesheets(items, "/about.html")).IsEqualTo("/styles.css");
            await Assert.That(Stylesheets(items, "/blog/index.html")).IsEqualTo("/styles.css,/blog/blog.css");
            await Assert.That(Stylesheets(items, "/blog/hello.html")).IsEqualTo("/styles.css,/blog/blog.css");

            await Assert.That(Stylesheets(items, "/blog/2026/index.html"))
                .IsEqualTo("/styles.css,/blog/blog.css,/blog/2026/year.css");
        }
    }

    /// <summary>
    ///     Verifies that a stylesheet applies below the directory declaring it and nowhere else, so that a
    ///     section can style itself without reaching the pages beside it.
    /// </summary>
    [Test]
    public async Task Scan_Test_Stylesheet_DoesNotReachASiblingBranch()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.CreateStyled();

        // Act
        IReadOnlyList<ContentItem> items = Scan(fixture);

        // Assert
        await Assert.That(items.Single(item => item.Href == "/about.html").Stylesheets)
            .DoesNotContain("/blog/blog.css");
    }

    /// <summary>
    ///     Verifies that a stylesheet is located as a copy in its own right, carrying no cascade of its own,
    ///     so that the file the links point at is published beside the pages that link it.
    /// </summary>
    [Test]
    public async Task Scan_Test_Stylesheet_IsLocatedAsACopy()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.CreateStyled();

        // Act
        IReadOnlyList<ContentItem> items = Scan(fixture);
        ContentItem stylesheet = items.Single(item => item.Href == "/styles.css");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(stylesheet.Kind).IsEqualTo(ContentKind.Stylesheet);
            await Assert.That(stylesheet.OutputPath).IsEqualTo("styles.css");
            await Assert.That(stylesheet.Stylesheets).IsEmpty();
        }
    }

    /// <summary>
    ///     Verifies that two sections styling themselves independently never see each other's stylesheet,
    ///     while both still inherit the one above them.
    /// </summary>
    /// <remarks>
    ///     Distinct from the sibling page above: this is the case that would break if the accumulator were
    ///     mutated in place rather than rebuilt per directory, because the first branch walked would leak
    ///     into every branch walked after it.
    /// </remarks>
    [Test]
    public async Task Scan_Test_SiblingDirectories_DoNotShareAStylesheet()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.CreateEmpty()
            .WriteFile("root.css", "body{margin:0}")
            .Write("index.md", "Home")
            .WriteFile("blog/styles1.css", "article{max-width:70ch}")
            .Write("blog/index.md", "Blog")
            .WriteFile("foo/styles2.css", "h1{color:teal}")
            .Write("foo/index.md", "Foo");

        // Act
        IReadOnlyList<ContentItem> items = Scan(fixture);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(Stylesheets(items, "/index.html")).IsEqualTo("/root.css");
            await Assert.That(Stylesheets(items, "/blog/index.html")).IsEqualTo("/root.css,/blog/styles1.css");
            await Assert.That(Stylesheets(items, "/foo/index.html")).IsEqualTo("/root.css,/foo/styles2.css");
        }
    }

    /// <summary>
    ///     Verifies that an extension is recognized whatever its case, so that a file is handled by what it
    ///     is rather than by how its name happens to be typed.
    /// </summary>
    /// <remarks>
    ///     Reachable on every platform, not only a case-insensitive one: the extension is read back as it was
    ///     written, so a file arriving from elsewhere carries whatever case it was created with.
    /// </remarks>
    [Test]
    public async Task Scan_Test_ExtensionCase_DoesNotChangeTheClassification()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.CreateEmpty()
            .Write("index.md", "Home")
            .Write("About.MD", "About")
            .WriteFile("Styles.CSS", "body{margin:0}")
            .WriteFile("App.JS", "globalThis.app = 1");

        // Act
        IReadOnlyList<ContentItem> items = Scan(fixture);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(items.Single(item => item.Href == "/About.html").Kind)
                .IsEqualTo(ContentKind.Page);

            await Assert.That(items.Single(item => item.Href == "/Styles.CSS").Kind)
                .IsEqualTo(ContentKind.Stylesheet);

            await Assert.That(items.Single(item => item.Href == "/App.JS").Kind)
                .IsEqualTo(ContentKind.Script);

            await Assert.That(Stylesheets(items, "/index.html")).IsEqualTo("/Styles.CSS");
            await Assert.That(Scripts(items, "/index.html")).IsEqualTo("/App.JS");
        }
    }

    /// <summary>
    ///     Verifies that the path of the base URL reaches a stylesheet link as well as a page link, so that
    ///     the two cannot disagree about where the site is served from.
    /// </summary>
    [Test]
    public async Task Scan_Test_BaseUrlPath_IsPrefixedToEveryStylesheet()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.CreateStyled().WithBaseUrl("https://example.com/gelyn");

        // Act
        IReadOnlyList<ContentItem> items = Scan(fixture);

        // Assert
        await Assert.That(Stylesheets(items, "/gelyn/blog/2026/index.html"))
            .IsEqualTo("/gelyn/styles.css,/gelyn/blog/blog.css,/gelyn/blog/2026/year.css");
    }

    /// <summary>
    ///     Verifies that a page links the script of every directory above it as well as its own, ordered
    ///     outermost first, which is the order a deferred script runs in.
    /// </summary>
    [Test]
    public async Task Scan_Test_Scripts_CascadeOutermostFirst()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.CreateScripted();

        // Act
        IReadOnlyList<ContentItem> items = Scan(fixture);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(Scripts(items, "/index.html")).IsEqualTo("/app.js");
            await Assert.That(Scripts(items, "/about.html")).IsEqualTo("/app.js");
            await Assert.That(Scripts(items, "/blog/index.html")).IsEqualTo("/app.js,/blog/blog.js");
            await Assert.That(Scripts(items, "/blog/hello.html")).IsEqualTo("/app.js,/blog/blog.js");

            await Assert.That(Scripts(items, "/blog/2026/index.html"))
                .IsEqualTo("/app.js,/blog/blog.js,/blog/2026/year.js");
        }
    }

    /// <summary>
    ///     Verifies that two sections scripting themselves independently never see each other's script, while
    ///     both still inherit the one above them.
    /// </summary>
    [Test]
    public async Task Scan_Test_SiblingDirectories_DoNotShareAScript()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.CreateEmpty()
            .WriteFile("app.js", "globalThis.app = 1")
            .Write("index.md", "Home")
            .WriteFile("blog/one.js", "globalThis.one = 1")
            .Write("blog/index.md", "Blog")
            .WriteFile("foo/two.js", "globalThis.two = 1")
            .Write("foo/index.md", "Foo");

        // Act
        IReadOnlyList<ContentItem> items = Scan(fixture);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(Scripts(items, "/index.html")).IsEqualTo("/app.js");
            await Assert.That(Scripts(items, "/blog/index.html")).IsEqualTo("/app.js,/blog/one.js");
            await Assert.That(Scripts(items, "/foo/index.html")).IsEqualTo("/app.js,/foo/two.js");
        }
    }

    /// <summary>
    ///     Verifies that a script is located as a copy in its own right, carrying no cascade of its own, so
    ///     that the file the tags point at is published beside the pages that load it.
    /// </summary>
    [Test]
    public async Task Scan_Test_Script_IsLocatedAsACopy()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.CreateScripted();

        // Act
        IReadOnlyList<ContentItem> items = Scan(fixture);
        ContentItem script = items.Single(item => item.Href == "/app.js");

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(script.Kind).IsEqualTo(ContentKind.Script);
            await Assert.That(script.OutputPath).IsEqualTo("app.js");
            await Assert.That(script.Scripts).IsEmpty();
        }
    }

    /// <summary>
    ///     Verifies that the two cascades stay apart, so that a stylesheet is never linked as a script and a
    ///     script is never linked as a stylesheet.
    /// </summary>
    /// <remarks>
    ///     The case the pair carried by one value exists to rule out: two lists of the same element type, in
    ///     one walk, which separate parameters would accept in either order.
    /// </remarks>
    [Test]
    public async Task Scan_Test_StylesheetsAndScripts_CascadeIndependently()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.CreateEmpty()
            .WriteFile("app.js", "globalThis.app = 1")
            .WriteFile("styles.css", "body{margin:0}")
            .Write("index.md", "Home")
            .WriteFile("blog/blog.css", "article{max-width:70ch}")
            .Write("blog/index.md", "Blog");

        // Act
        IReadOnlyList<ContentItem> items = Scan(fixture);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(Stylesheets(items, "/blog/index.html")).IsEqualTo("/styles.css,/blog/blog.css");
            await Assert.That(Scripts(items, "/blog/index.html")).IsEqualTo("/app.js");
        }
    }

    /// <summary>
    ///     Verifies that the path of the base URL reaches a script tag as well as a page link, so that the two
    ///     cannot disagree about where the site is served from.
    /// </summary>
    [Test]
    public async Task Scan_Test_BaseUrlPath_IsPrefixedToEveryScript()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.CreateScripted().WithBaseUrl("https://example.com/gelyn");

        // Act
        IReadOnlyList<ContentItem> items = Scan(fixture);

        // Assert
        await Assert.That(Scripts(items, "/gelyn/blog/2026/index.html"))
            .IsEqualTo("/gelyn/app.js,/gelyn/blog/blog.js,/gelyn/blog/2026/year.js");
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

    // Joined rather than compared as a sequence, because the assertion is about order and IsEquivalentTo
    // ignores it.
    private static string Stylesheets(IReadOnlyList<ContentItem> items, string href) =>
        string.Join(',', items.Single(item => item.Href == href).Stylesheets);

    private static string Scripts(IReadOnlyList<ContentItem> items, string href) =>
        string.Join(',', items.Single(item => item.Href == href).Scripts);

    private static string Scripts(IReadOnlyList<ContentPage> pages, string href) =>
        string.Join(',', pages.Single(page => page.Href == href).Scripts);

    private static Task<IReadOnlyList<ContentPage>> RenderAsync(ContentFixture fixture) =>
        fixture.Pipeline.RenderAsync(Scan(fixture), CancellationToken.None);

    #endregion
}
