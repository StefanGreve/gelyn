using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

using Gelyn.Core.Internals;
using Gelyn.Model;
using Gelyn.Services;
using Gelyn.Tests.Fixtures;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Gelyn.Tests.Services;

/// <summary>
///     Unit tests for the <see cref="SiteBuilder"/> class.
/// </summary>
public partial class SiteBuilderTests
{
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";

    #region BuildAsync Tests

    /// <summary>
    ///     Verifies that the landing page is written before every other page.
    /// </summary>
    [Test]
    public async Task BuildAsync_Test_LandingPage_IsWrittenFirst()
    {
        // Arrange
        var fixture = ContentFixture.Create();

        // Act
        BuildReport report = await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(report.Pages.Count).IsEqualTo(6);
            await Assert.That(report.Pages[0]).IsEqualTo("index.html");
        }
    }

    /// <summary>
    ///     Verifies that a file which is not Markdown is copied to the mirrored output path with its text
    ///     intact, and reported apart from the pages that were rendered.
    /// </summary>
    [Test]
    public async Task BuildAsync_Test_MiscFile_IsCopiedToTheMirroredPath()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.Create()
            .WriteFile("robots.txt", "User-agent: *")
            .WriteFile("blog/notice.txt", "Archived.");

        // Act
        BuildReport report = await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(report.Copies)
                .IsEquivalentTo(["robots.txt", Path.Combine("blog", "notice.txt")]);

            await Assert.That(report.Pages.Count).IsEqualTo(6);

            await Assert.That(fixture.ReadOutput(Path.Combine(fixture.OutputRoot, "robots.txt")))
                .IsEqualTo("User-agent: *");

            await Assert.That(fixture.ReadOutput(Path.Combine(fixture.OutputRoot, "blog", "notice.txt")))
                .IsEqualTo("Archived.");
        }
    }

    /// <summary>
    ///     Verifies that a binary file survives the copy byte for byte, which is what the copy buys over
    ///     reading the file as text and writing it back out.
    /// </summary>
    [Test]
    public async Task BuildAsync_Test_BinaryFile_IsCopiedByteForByte()
    {
        // Arrange
        byte[] expected = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0xFF];
        ContentFixture fixture = ContentFixture.Create().WriteBytes("logo.png", expected);

        // Act
        await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        byte[] actual = await fixture.FileSystem.File
            .ReadAllBytesAsync(Path.Combine(fixture.OutputRoot, "logo.png"), CancellationToken.None);

        await Assert.That(actual).IsEquivalentTo(expected);
    }

    /// <summary>
    ///     Verifies that a page links every stylesheet that applies to it, in the order the cascade requires,
    ///     and that a page above them links only what applies at its own level.
    /// </summary>
    [Test]
    public async Task BuildAsync_Test_StylesheetLinks_AreEmittedOutermostFirst()
    {
        // Arrange
        var fixture = ContentFixture.CreateStyled();

        // Act
        await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ReadStylesheets(fixture, "index.html")).IsEqualTo("/styles.css");

            await Assert.That(ReadStylesheets(fixture, Path.Combine("blog", "2026", "index.html")))
                .IsEqualTo("/styles.css,/blog/blog.css,/blog/2026/year.css");
        }
    }

    /// <summary>
    ///     Verifies that a page emits one deferred tag per script that applies to it, in the order the cascade
    ///     requires, and that a page above them emits only what applies at its own level.
    /// </summary>
    [Test]
    public async Task BuildAsync_Test_ScriptTags_AreDeferredAndOutermostFirst()
    {
        // Arrange
        var fixture = ContentFixture.CreateScripted();

        // Act
        await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ReadScripts(fixture, "index.html")).IsEqualTo("/app.js");

            await Assert.That(ReadScripts(fixture, Path.Combine("blog", "2026", "index.html")))
                .IsEqualTo("/app.js,/blog/blog.js,/blog/2026/year.js");

            await Assert.That(ReadPage(fixture, "index.html")).Contains("""<script defer src="/app.js">""");
        }
    }

    /// <summary>
    ///     Verifies that a script is published alongside the pages loading it, which the copy loop has to
    ///     cover for the tags to resolve to anything.
    /// </summary>
    [Test]
    public async Task BuildAsync_Test_Script_IsCopiedBesideThePagesThatLoadIt()
    {
        // Arrange
        var fixture = ContentFixture.CreateScripted();

        // Act
        BuildReport report = await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(report.Copies).IsEquivalentTo([
                "app.js",
                Path.Combine("blog", "blog.js"),
                Path.Combine("blog", "2026", "year.js"),
            ]);

            await Assert.That(fixture.ReadOutput(Path.Combine(fixture.OutputRoot, "app.js")))
                .IsEqualTo("globalThis.app = 1");
        }
    }

    /// <summary>
    ///     Verifies that a page enabling math rendering loads the KaTeX stylesheet, the library and the
    ///     auto-render extension that activates it.
    /// </summary>
    [Test]
    public async Task BuildAsync_Test_MathRenderer_IsLoadedByThePageThatEnablesIt()
    {
        // Arrange
        var fixture = ContentFixture.CreateMathematical();
        string katex = Package("katex", fixture.Options.KaTeXVersion);

        // Act
        await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        string page = Path.Combine("blog", "index.html");

        using (Assert.Multiple())
        {
            await Assert.That(ReadMathAssets(fixture, page))
                .IsEqualTo($"{katex}/katex.min.css,{katex}/katex.min.js,{katex}/contrib/auto-render.min.js");

            await Assert.That(ReadPage(fixture, page))
                .Contains("""onload="renderMathInElement(document.body);"></script>""");
        }
    }

    /// <summary>
    ///     Verifies that math rendering applies to the page declaring it alone, neither to the pages beside it
    ///     nor to the pages below it.
    /// </summary>
    [Test]
    public async Task BuildAsync_Test_MathRenderer_IsInheritedByNoPage()
    {
        // Arrange
        var fixture = ContentFixture.CreateMathematical();

        // Act
        await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ReadMathAssets(fixture, "index.html")).IsEmpty();
            await Assert.That(ReadMathAssets(fixture, Path.Combine("blog", "hello.html"))).IsEmpty();
            await Assert.That(ReadMathAssets(fixture, Path.Combine("blog", "2026", "index.html"))).IsEmpty();
        }
    }

    /// <summary>
    ///     Verifies that the configured version is the one requested, so that a newer KaTeX needs no new build
    ///     of the tool.
    /// </summary>
    [Test]
    public async Task BuildAsync_Test_MathRenderer_RequestsTheConfiguredVersion()
    {
        // Arrange
        var fixture = ContentFixture.CreateMathematical();
        fixture.Options.KaTeXVersion = "1.2.3";

        // Act
        await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        await Assert.That(ReadMathAssets(fixture, Path.Combine("blog", "index.html")))
            .DoesNotContain("0.19.0")
            .And.StartsWith(Package("katex", "1.2.3"));
    }

    /// <summary>
    ///     Verifies that the math renderer precedes the resources the content tree supplies, so that a
    ///     stylesheet of the author's overrides KaTeX and a script of the author's runs once it has loaded.
    /// </summary>
    [Test]
    public async Task BuildAsync_Test_MathRenderer_PrecedesTheCascadedResources()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.CreateMathematical()
            .WriteFile("styles.css", "body{margin:0}")
            .WriteFile("app.js", "globalThis.app = 1");

        string katex = Package("katex", fixture.Options.KaTeXVersion);

        // Act
        await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        string page = Path.Combine("blog", "index.html");

        using (Assert.Multiple())
        {
            await Assert.That(ReadStylesheets(fixture, page)).IsEqualTo($"{katex}/katex.min.css,/styles.css");
            await Assert.That(ReadScripts(fixture, page)).IsEqualTo($"{katex}/katex.min.js,/app.js");
        }
    }

    /// <summary>
    ///     Verifies that a page enabling diagram rendering imports Mermaid as a module and initializes it.
    /// </summary>
    [Test]
    public async Task BuildAsync_Test_DiagramRenderer_IsLoadedByThePageThatEnablesIt()
    {
        // Arrange
        var fixture = ContentFixture.CreateDiagrammatic();
        string mermaid = Package("mermaid", fixture.Options.MermaidVersion);

        // Act
        await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        string page = Path.Combine("blog", "index.html");

        using (Assert.Multiple())
        {
            await Assert.That(ReadDiagramImport(fixture, page)).IsEqualTo($"{mermaid}/mermaid.esm.min.mjs");

            await Assert.That(ReadPage(fixture, page))
                .Contains("""<script type="module">""")
                .And.Contains("mermaid.initialize({ startOnLoad: true });");
        }
    }

    /// <summary>
    ///     Verifies that diagram rendering applies to the page declaring it alone, neither to the pages beside
    ///     it nor to the pages below it.
    /// </summary>
    [Test]
    public async Task BuildAsync_Test_DiagramRenderer_IsInheritedByNoPage()
    {
        // Arrange
        var fixture = ContentFixture.CreateDiagrammatic();

        // Act
        await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ReadDiagramImport(fixture, "index.html")).IsNull();
            await Assert.That(ReadDiagramImport(fixture, Path.Combine("blog", "hello.html"))).IsNull();
            await Assert.That(ReadDiagramImport(fixture, Path.Combine("blog", "2026", "index.html"))).IsNull();
        }
    }

    /// <summary>
    ///     Verifies that the configured version is the one imported, so that a newer Mermaid needs no new build
    ///     of the tool.
    /// </summary>
    [Test]
    public async Task BuildAsync_Test_DiagramRenderer_RequestsTheConfiguredVersion()
    {
        // Arrange
        var fixture = ContentFixture.CreateDiagrammatic();
        fixture.Options.MermaidVersion = "11.0.0";

        // Act
        await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        await Assert.That(ReadDiagramImport(fixture, Path.Combine("blog", "index.html")))
            .IsEqualTo($"{Package("mermaid", "11.0.0")}/mermaid.esm.min.mjs");
    }

    /// <summary>
    ///     Verifies that each renderer answers to its own key, so that a page carrying math pays nothing for
    ///     diagrams and the other way round.
    /// </summary>
    [Test]
    public async Task BuildAsync_Test_Renderers_AreEnabledIndependently()
    {
        // Arrange
        var mathematical = ContentFixture.CreateMathematical();
        var diagrammatic = ContentFixture.CreateDiagrammatic();
        string page = Path.Combine("blog", "index.html");

        // Act
        await mathematical.Builder.BuildAsync(CancellationToken.None);
        await diagrammatic.Builder.BuildAsync(CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ReadDiagramImport(mathematical, page)).IsNull();
            await Assert.That(ReadMathAssets(diagrammatic, page)).IsEmpty();
        }
    }

    /// <summary>
    ///     Verifies that a stylesheet is published alongside the pages linking it, which the copy loop has to
    ///     cover for the links to resolve to anything.
    /// </summary>
    [Test]
    public async Task BuildAsync_Test_Stylesheet_IsCopiedBesideThePagesThatLinkIt()
    {
        // Arrange
        var fixture = ContentFixture.CreateStyled();

        // Act
        BuildReport report = await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(report.Copies).IsEquivalentTo([
                "styles.css",
                Path.Combine("blog", "blog.css"),
                Path.Combine("blog", "2026", "year.css"),
            ]);

            await Assert.That(fixture.ReadOutput(Path.Combine(fixture.OutputRoot, "styles.css")))
                .IsEqualTo("body{margin:0}");
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
        var fixture = ContentFixture.Create();
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

    #region Shared Fragment Tests

    /// <summary>
    ///     Verifies that every page marks its own navigation entry as current and no other, and that a page the
    ///     navigation leaves out marks none at all.
    /// </summary>
    /// <remarks>
    ///     The banner is rendered once per variant and not once per page, so this is what prevents one
    ///     page's current entry from being served to the rest of the site.
    /// </remarks>
    [Test]
    public async Task BuildAsync_Test_Header_MarksTheCurrentPageOnly()
    {
        // Arrange
        var fixture = ContentFixture.Create();
        string nested = fixture.FileSystem.Path.Combine("blog", "hello.html");
        string section = fixture.FileSystem.Path.Combine("blog", "index.html");

        // Act
        await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(CurrentEntry(fixture, "index.html")).IsEqualTo("/index.html");
            await Assert.That(CurrentEntry(fixture, "about.html")).IsEqualTo("/about.html");
            await Assert.That(CurrentEntry(fixture, section)).IsEqualTo("/blog/index.html");
            await Assert.That(CurrentEntry(fixture, nested)).IsNull();
        }
    }

    /// <summary>
    ///     Verifies that a page's footer carries the date that page declares, and that a page declaring none
    ///     carries no date at all, never one taken from elsewhere.
    /// </summary>
    [Test]
    public async Task BuildAsync_Test_Footer_CarriesThePageDate()
    {
        // Arrange
        var fixture = ContentFixture.CreateDated();

        // Act
        await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        string undated = ReadFooter(fixture, Path.Combine("blog", "hello.html"));

        using (Assert.Multiple())
        {
            await Assert.That(ReadFooter(fixture, "index.html"))
                .Contains("""last revised <time datetime="2026-02-01">Feb 1, 2026</time>""");
            await Assert.That(ReadFooter(fixture, Path.Combine("blog", "index.html")))
                .Contains("""last revised <time datetime="2026-03-15">Mar 15, 2026</time>""");
            await Assert.That(undated).Contains("Built with Gelyn");
            await Assert.That(undated).DoesNotContain("last revised");
        }
    }

    /// <summary>
    ///     Verifies that a page declaring a revision dates its footer and its listing from different dates:
    ///     the footer from the revision, the table of documents from the publication.
    /// </summary>
    /// <remarks>
    ///     The one case that tells the two dates apart end to end. Every other page in the suite declares a
    ///     publication date alone, where the fallback makes the two indistinguishable.
    /// </remarks>
    [Test]
    public async Task BuildAsync_Test_Revision_DatesTheFooterAndNotTheListing()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.CreateEmpty()
            .WriteDated("index.md", "Home", "2026-01-01", "[TOD]")
            .WriteRevised("about.md", "About", "2026-02-01", "2026-08-20");

        // Act
        await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ReadFooter(fixture, "about.html"))
                .Contains("""last revised <time datetime="2026-08-20">Aug 20, 2026</time>""");

            await Assert.That(ReadTable(fixture, "index.html"))
                .Contains("""<time datetime="2026-02-01">2026-02-01</time>""")
                .And.DoesNotContain("2026-08-20");
        }
    }

    /// <summary>
    ///     Verifies that two pages declaring the same date carry the same footer while pages declaring
    ///     different dates do not, so the footer varies by the declared date and by nothing else.
    /// </summary>
    [Test]
    public async Task BuildAsync_Test_Footer_VariesByDateAlone()
    {
        // Arrange
        var fixture = ContentFixture.CreateDated();

        // Act
        await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        string home = ReadFooter(fixture, "index.html");
        string about = ReadFooter(fixture, "about.html");
        string section = ReadFooter(fixture, Path.Combine("blog", "index.html"));
        string undated = ReadFooter(fixture, Path.Combine("blog", "hello.html"));

        string[] footers = [home, about, section, undated];

        using (Assert.Multiple())
        {
            await Assert.That(about).IsEqualTo(home);
            await Assert.That(footers.Distinct().Count()).IsEqualTo(3);
        }
    }

    /// <summary>
    ///     Verifies that the banner precedes the content and the footer follows it.
    /// </summary>
    /// <remarks>
    ///     Both fragments are arguments now, so the layout can no longer guarantee their order by construction:
    ///     passing them the wrong way round compiles and produces a document that is still well formed.
    /// </remarks>
    [Test]
    public async Task BuildAsync_Test_SharedFragments_SurroundTheContent()
    {
        // Arrange
        var fixture = ContentFixture.Create();

        // Act
        await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        string html = ReadPage(fixture, "index.html");

        using (Assert.Multiple())
        {
            await Assert.That(html.IndexOf("<header>", StringComparison.Ordinal))
                .IsLessThan(html.IndexOf("<main>", StringComparison.Ordinal));

            await Assert.That(html.IndexOf("<main>", StringComparison.Ordinal))
                .IsLessThan(html.IndexOf("<footer>", StringComparison.Ordinal));
        }
    }

    #endregion // Shared Fragment Tests

    #region Table Of Documents Tests

    /// <summary>
    ///     Verifies that a marker is replaced by a table listing the marker's own directory, newest first,
    ///     with nothing of the marker left behind.
    /// </summary>
    [Test]
    public async Task BuildAsync_Test_Marker_IsReplacedByTheTable()
    {
        // Arrange
        var fixture = ContentFixture.CreateTabulated();
        string section = Path.Combine("blog", "index.html");

        // Act
        await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(TableLinks(fixture, section))
                .IsEqualTo("/blog/2026/index.html,/blog/hello.html");

            await Assert.That(ReadPage(fixture, section)).DoesNotContain("[TOD]");
        }
    }

    /// <summary>
    ///     Verifies that an index one directory below the marker is marked as a folder while every entry
    ///     beside the marker is marked as a file, which is the only automated check the theme rules have.
    /// </summary>
    [Test]
    public async Task BuildAsync_Test_NestedIndex_IsMarkedAsAFolder()
    {
        // Arrange
        var fixture = ContentFixture.CreateTabulated();

        // Act
        await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        await Assert.That(TableTypes(fixture, Path.Combine("blog", "index.html")))
            .IsEqualTo("folder,file");
    }

    /// <summary>
    ///     Verifies that a marker on one page leaves every other page of the site byte for byte as it was.
    /// </summary>
    /// <remarks>
    ///     The substitution runs during compose, which every page passes through, so this is what keeps one
    ///     page's table from reaching the rest of the site.
    /// </remarks>
    [Test]
    public async Task BuildAsync_Test_PagesWithoutAMarker_AreUnchanged()
    {
        // Arrange
        var marked = ContentFixture.CreateTabulated();
        ContentFixture plain = ContentFixture.CreateTabulated().WriteDated("blog/index.md", "Blog", "2026-02-01");
        string section = Path.Combine("blog", "index.html");

        string[] others =
        [
            "index.html",
            "about.html",
            Path.Combine("blog", "hello.html"),
            Path.Combine("blog", "draft.html"),
            Path.Combine("blog", "2026", "index.html"),
            Path.Combine("blog", "2026", "retro.html"),
            Path.Combine("blogs", "other.html"),
        ];

        // Act
        await marked.Builder.BuildAsync(CancellationToken.None);
        await plain.Builder.BuildAsync(CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            foreach (string path in others)
                await Assert.That(ReadPage(marked, path)).IsEqualTo(ReadPage(plain, path));

            // Without this the test would pass just as well on a build that substituted nothing anywhere.
            await Assert.That(ReadPage(marked, section)).IsNotEqualTo(ReadPage(plain, section));
        }
    }

    /// <summary>
    ///     Verifies that a marker written inside a fenced block or a code span is left alone while a real
    ///     marker on the same page is still expanded.
    /// </summary>
    /// <remarks>
    ///     The whole choice of matching the rendered HTML rests on this: Markdig escapes the brackets of both
    ///     forms, so a marker shown as an example in the documentation cannot be substituted.
    /// </remarks>
    [Test]
    public async Task BuildAsync_Test_EscapedMarker_IsLeftAlone()
    {
        // Arrange
        string body = string.Join(
            Environment.NewLine,
            "[TOD]",
            string.Empty,
            "An inline `[TOD]` stays code.",
            string.Empty,
            "```",
            "[TOD]",
            "```");

        ContentFixture fixture = ContentFixture.CreateEmpty()
            .WriteDated("index.md", "Home", "2026-01-01", body)
            .WriteDated("about.md", "About", "2026-01-15");

        // Act
        await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        string html = ReadPage(fixture, "index.html");

        using (Assert.Multiple())
        {
            await Assert.That(TableLinks(fixture, "index.html")).IsEqualTo("/about.html");
            await Assert.That(html).Contains("<code>[TOD]</code>");
            await Assert.That(html).Contains("<pre><code>[TOD]");
        }
    }

    #endregion // Table Of Documents Tests

    #region Canonical Tests

    /// <summary>
    ///     Verifies that each page states its own absolute address once a base URL is configured, and that the
    ///     address agrees with the path prefix the same base URL puts on every link.
    /// </summary>
    [Test]
    public async Task BuildAsync_Test_Canonical_AddressesEachPageAbsolutely()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.Create().WithBaseUrl("https://example.com/gelyn/docs");
        string deep = fixture.FileSystem.Path.Combine("blog", "2026", "q3", "deep.html");

        // Act
        await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ReadCanonical(fixture, "index.html"))
                .IsEqualTo("https://example.com/gelyn/docs/index.html");

            await Assert.That(ReadCanonical(fixture, deep))
                .IsEqualTo("https://example.com/gelyn/docs/blog/2026/q3/deep.html");
        }
    }

    /// <summary>
    ///     Verifies that no page states a canonical address when no base URL is configured, because a relative
    ///     one carries no meaning and a guessed one would be worse than none.
    /// </summary>
    /// <remarks>
    ///     The count is pinned before the loop, because an empty output would satisfy the loop on its own.
    /// </remarks>
    [Test]
    public async Task BuildAsync_Test_Canonical_IsOmittedWithoutABaseUrl()
    {
        // Arrange
        var fixture = ContentFixture.Create();

        // Act
        await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(fixture.EnumerateOutput().Count()).IsEqualTo(6);

            foreach (string page in fixture.EnumerateOutput())
                await Assert.That(Canonical(fixture.ReadOutput(page))).IsNull();
        }
    }

    #endregion // Canonical Tests

    #region Feed Tests

    /// <summary>
    ///     Verifies that the feed is written beside the pages, carries the pages that opt into it newest
    ///     first, and is reported apart from them.
    /// </summary>
    [Test]
    public async Task BuildAsync_Test_Feed_PublishesThePagesThatOptIn()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.CreatePublished()
            .WithBaseUrl("https://example.com")
            .WithFeed();

        // Act
        BuildReport report = await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(report.Generated).IsEquivalentTo(["feed.xml"]);
            await Assert.That(fixture.OutputHolds("feed.xml")).IsTrue();
            await Assert.That(EntryTitles(fixture)).IsEqualTo("Later,Hello");

            await Assert.That(EntryLinks(fixture))
                .IsEqualTo("https://example.com/blog/later.html,https://example.com/blog/hello.html");
        }
    }

    /// <summary>
    ///     Verifies that the feed is counted among the files the build produced, so that the total the command
    ///     prints accounts for the output no source stands behind.
    /// </summary>
    [Test]
    public async Task BuildAsync_Test_Feed_IsCountedAmongTheFilesWritten()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.CreatePublished()
            .WithBaseUrl("https://example.com")
            .WithFeed();

        // Act
        BuildReport report = await fixture.Builder.BuildAsync(CancellationToken.None);
        int written = report.Pages.Count + report.Generated.Count;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(report.Pages.Count).IsEqualTo(5);
            await Assert.That(report.FileCount).IsEqualTo(written);
            await Assert.That(Count(fixture, EventIds.WRITE_COMPLETED, "FileCount")).IsEqualTo(written);
            await Assert.That(Count(fixture, EventIds.BUILD_COMPLETED, "FileCount")).IsEqualTo(written);
        }
    }

    /// <summary>
    ///     Verifies that a page opting into the feed produces nothing while the setting is off, which is the
    ///     default, and that the build says nothing about it either.
    /// </summary>
    [Test]
    public async Task BuildAsync_Test_Feed_IsNotWrittenWhileTheSettingIsOff()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.CreatePublished().WithBaseUrl("https://example.com");

        // Act
        BuildReport report = await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(report.Generated).IsEmpty();
            await Assert.That(fixture.OutputHolds("feed.xml")).IsFalse();
            await Assert.That(Warnings(fixture)).IsEmpty();
        }
    }

    /// <summary>
    ///     Verifies that a feed asked for without a base URL is skipped with one warning naming that reason,
    ///     and that the build still succeeds, because the workflows supply the base URL per run.
    /// </summary>
    [Test]
    public async Task BuildAsync_Test_Feed_IsSkippedWithoutABaseUrl()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.CreatePublished().WithFeed();

        // Act
        BuildReport report = await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(report.Generated).IsEmpty();
            await Assert.That(fixture.OutputHolds("feed.xml")).IsFalse();
            await Assert.That(report.Pages.Count).IsEqualTo(5);

            await Assert.That(Reported(fixture, EventIds.FEED_SKIPPED_WITHOUT_BASE_URL).Level)
                .IsEqualTo(LogLevel.Warning);

            // The missing origin is what the author fixes, so the pages are not worth a warning of their own.
            await Assert.That(Reports(fixture, EventIds.FEED_ENTRY_SKIPPED)).IsEmpty();
        }
    }

    /// <summary>
    ///     Verifies that a feed no page opts into is skipped with one warning naming that reason, which is the
    ///     other way the feature produces nothing while it is switched on.
    /// </summary>
    [Test]
    public async Task BuildAsync_Test_Feed_IsSkippedWhenNoPageOptsIn()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.CreateDated()
            .WithBaseUrl("https://example.com")
            .WithFeed();

        // Act
        BuildReport report = await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(report.Generated).IsEmpty();
            await Assert.That(fixture.OutputHolds("feed.xml")).IsFalse();

            await Assert.That(Reported(fixture, EventIds.FEED_SKIPPED_WITHOUT_ENTRIES).Level)
                .IsEqualTo(LogLevel.Warning);
        }
    }

    /// <summary>
    ///     Verifies that a page opting into the feed without declaring a date is named by a warning of its
    ///     own, and left out of the feed the pages beside it still fill.
    /// </summary>
    /// <remarks>
    ///     The warning names the source file and not the generated one, because the declaration is edited
    ///     there. It is the only reason <see cref="Gelyn.Model.ContentPage"/> carries a source path at all.
    /// </remarks>
    [Test]
    public async Task BuildAsync_Test_Feed_WarnsAboutAPageDeclaringNoDate()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.CreatePublished()
            .WithBaseUrl("https://example.com")
            .WithFeed();

        string source = Path.Combine(fixture.ContentRoot, "blog", "draft.md");

        // Act
        await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        FakeLogRecord warning = Reported(fixture, EventIds.FEED_ENTRY_SKIPPED);

        using (Assert.Multiple())
        {
            await Assert.That(warning.Level).IsEqualTo(LogLevel.Warning);
            await Assert.That(warning.GetStructuredStateValue("Path")).IsEqualTo(source);
            await Assert.That(EntryTitles(fixture)).DoesNotContain("Draft");
        }
    }

    /// <summary>
    ///     Verifies that the feed survives the build that follows the one writing it, which no source
    ///     accounts for and the reconcile would otherwise delete.
    /// </summary>
    [Test]
    public async Task BuildAsync_Test_Feed_SurvivesTheNextBuild()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.CreatePublished()
            .WithBaseUrl("https://example.com")
            .WithFeed();

        BuildReport first = await fixture.Builder.BuildAsync(CancellationToken.None);
        string before = ReadFeed(fixture);

        // Act
        BuildReport second = await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(second.Removed).IsEmpty();
            await Assert.That(second.Generated).IsEquivalentTo(first.Generated);
            await Assert.That(fixture.OutputHolds("feed.xml")).IsTrue();

            // Dated from the newest entry and never from the clock, which is what keeps the bytes identical.
            await Assert.That(ReadFeed(fixture)).IsEqualTo(before);
        }
    }

    /// <summary>
    ///     Verifies that a feed left behind by a build that had the setting on is deleted once it is off,
    ///     because the seam exempts the output the build generated and not whatever happens to carry the name.
    /// </summary>
    [Test]
    public async Task BuildAsync_Test_Feed_IsRemovedOnceTheSettingIsOff()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.CreatePublished()
            .WithBaseUrl("https://example.com")
            .WithFeed();

        await fixture.Builder.BuildAsync(CancellationToken.None);

        fixture.Options.EnableFeed = false;

        // Act
        BuildReport report = await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(report.Removed).IsEquivalentTo(["feed.xml"]);
            await Assert.That(fixture.OutputHolds("feed.xml")).IsFalse();
        }
    }

    #endregion // Feed Tests

    #region Reconcile Tests

    /// <summary>
    ///     Verifies that the output of a page whose source is gone is deleted and reported.
    /// </summary>
    [Test]
    public async Task BuildAsync_Test_OrphanedPage_IsRemoved()
    {
        // Arrange
        var fixture = ContentFixture.Create();
        await fixture.Builder.BuildAsync(CancellationToken.None);

        fixture.Delete("about.md");

        // Act
        BuildReport report = await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(report.Removed).IsEquivalentTo(["about.html"]);
            await Assert.That(fixture.OutputHolds("about.html")).IsFalse();
            await Assert.That(fixture.OutputHolds("index.html")).IsTrue();
        }
    }

    /// <summary>
    ///     Verifies that a colocated file is reconciled in the same way as a page, because every file the scan
    ///     locates is an output.
    /// </summary>
    [Test]
    public async Task BuildAsync_Test_OrphanedCopy_IsRemoved()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.Create().WriteFile("robots.txt", "User-agent: *");
        await fixture.Builder.BuildAsync(CancellationToken.None);

        fixture.Delete("robots.txt");

        // Act
        BuildReport report = await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(report.Removed).IsEquivalentTo(["robots.txt"]);
            await Assert.That(fixture.OutputHolds("robots.txt")).IsFalse();
        }
    }

    /// <summary>
    ///     Verifies that a source the exclude patterns start matching has its output removed, which is what
    ///     proves the reconcile subtracts what the scan reports and not what is still on disk.
    /// </summary>
    [Test]
    public async Task BuildAsync_Test_NewlyExcludedSource_HasItsOutputRemoved()
    {
        // Arrange
        var fixture = ContentFixture.Create();
        await fixture.Builder.BuildAsync(CancellationToken.None);

        fixture.WithExcludePatterns("about.md");

        // Act
        BuildReport report = await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(report.Removed).IsEquivalentTo(["about.html"]);
            await Assert.That(fixture.OutputHolds("about.html")).IsFalse();
        }
    }

    /// <summary>
    ///     Verifies that a file no build ever wrote is deleted, so that output placed by hand cannot outlive
    ///     the directory it sits in.
    /// </summary>
    /// <remarks>
    ///     The nested path is composed instead of written out, because the report uses the native separator
    ///     while the fixture takes forward slashes.
    /// </remarks>
    [Test]
    public async Task BuildAsync_Test_StrayFile_IsRemoved()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.Create()
            .WriteOutput("leftover.html", "<p>From a build that is no longer the current one.</p>")
            .WriteOutput("blog/leftover.html", "<p>Nested, so the walk has to reach it.</p>");

        // Act
        BuildReport report = await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        string nested = fixture.FileSystem.Path.Combine("blog", "leftover.html");

        using (Assert.Multiple())
        {
            await Assert.That(report.Removed).IsEquivalentTo(["leftover.html", nested]);
            await Assert.That(fixture.OutputHolds("leftover.html")).IsFalse();
            await Assert.That(fixture.OutputHolds("blog/leftover.html")).IsFalse();
        }
    }

    /// <summary>
    ///     Verifies that a rebuild over an unchanged tree deletes nothing, which is what keeps the reconcile
    ///     from being destructive in the ordinary case.
    /// </summary>
    [Test]
    public async Task BuildAsync_Test_UnchangedTree_RemovesNothing()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.Create().WriteFile("robots.txt", "User-agent: *");
        BuildReport first = await fixture.Builder.BuildAsync(CancellationToken.None);

        // Act
        BuildReport second = await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(second.Removed.Count).IsEqualTo(0);
            await Assert.That(second.Pages).IsEquivalentTo(first.Pages);
            await Assert.That(second.Copies).IsEquivalentTo(first.Copies);
            await Assert.That(fixture.EnumerateOutput().Count()).IsEqualTo(6);
        }
    }

    /// <summary>
    ///     Verifies that a directory the reconcile empties is removed along with the files in it, while the
    ///     directory above it survives because it still holds a page.
    /// </summary>
    [Test]
    public async Task BuildAsync_Test_EmptiedDirectory_IsRemoved()
    {
        // Arrange
        var fixture = ContentFixture.Create();
        await fixture.Builder.BuildAsync(CancellationToken.None);

        fixture.Delete("blog/2026/q3/deep.md");

        // Act
        BuildReport report = await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(report.Removed.Count).IsEqualTo(1);
            await Assert.That(fixture.OutputHoldsDirectory("blog/2026/q3")).IsFalse();
            await Assert.That(fixture.OutputHoldsDirectory("blog/2026")).IsTrue();
            await Assert.That(fixture.OutputHolds("blog/2026/index.html")).IsTrue();
        }
    }

    /// <summary>
    ///     Verifies that a dot-prefixed entry survives at the root and nested, together with everything below
    ///     it. A <c>_site</c> deployed as a git worktree holds <c>.git</c> and a GitHub Pages site holds
    ///     <c>.nojekyll</c>, and deleting either would be reported as a feature.
    /// </summary>
    [Test]
    public async Task BuildAsync_Test_DotPrefixedEntry_Survives()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.Create()
            .WriteOutput(".nojekyll")
            .WriteOutput(".git/HEAD", "ref: refs/heads/gh-pages")
            .WriteOutput("blog/.keep");

        // Act
        BuildReport report = await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(report.Removed.Count).IsEqualTo(0);
            await Assert.That(fixture.OutputHolds(".nojekyll")).IsTrue();
            await Assert.That(fixture.OutputHolds(".git/HEAD")).IsTrue();
            await Assert.That(fixture.OutputHolds("blog/.keep")).IsTrue();
        }
    }

    /// <summary>
    ///     Verifies that a directory symbolic link in the output is not descended into, so that the reconcile
    ///     cannot delete a file outside the output directory.
    /// </summary>
    /// <remarks>
    ///     Driven against the real file system, because <c>MockFileSystem</c> does not resolve traversal
    ///     through a link, so it yields nothing on the far side whether the guard is present or not.
    /// </remarks>
    [Test]
    public async Task BuildAsync_Test_DirectorySymlinkInOutput_IsNotFollowed()
    {
        // Arrange
        Skip.When(OperatingSystem.IsWindows(), "Creating a directory symlink needs elevation on Windows.");

        using DiskFixture fixture = DiskFixture.Create()
            .Write("index.md", "---\ntitle: Home\n---")
            .WriteOutside("outside/keepsake.txt", "Not the build's to delete.")
            .LinkOutputOutside("shared", "outside");

        // Act
        BuildReport report = await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(fixture.Holds("outside/keepsake.txt")).IsTrue();
            await Assert.That(report.Removed.Count).IsEqualTo(0);
        }
    }

    #endregion // Reconcile Tests

    #region Counter Tests

    /// <summary>
    ///     Verifies that the build reports one counter per phase, in the order the phases run.
    /// </summary>
    /// <remarks>
    ///     Compared as a joined string, because <c>IsEquivalentTo</c> disregards the order under test.
    /// </remarks>
    [Test]
    public async Task BuildAsync_Test_Counters_AreReportedInPhaseOrder()
    {
        // Arrange
        var fixture = ContentFixture.Create();

        int[] phases =
        [
            EventIds.SCAN_COMPLETED,
            EventIds.RENDER_COMPLETED,
            EventIds.COMPOSE_COMPLETED,
            EventIds.WRITE_COMPLETED,
            EventIds.REMOVE_COMPLETED,
            EventIds.BUILD_COMPLETED,
        ];

        // Act
        await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        string reported = string.Join(',', fixture.Records.Select(static record => record.Id.Id));

        await Assert.That(reported).IsEqualTo(string.Join(',', phases));
    }

    /// <summary>
    ///     Verifies that the render and compose counters report the pages alone while the scan, write and
    ///     build counters report the copies alongside them, so that each counter means what its message says.
    /// </summary>
    /// <remarks>
    ///     Driven against a tree holding something to copy, which is what tells the item count, the page
    ///     count and the file count apart; a tree of pages alone satisfies every counter with one number.
    ///     <see cref="BuildAsync_Test_Counters_CountRemovalsApartFromWrites"/> is the case that separates the
    ///     removals from all three.
    /// </remarks>
    [Test]
    public async Task BuildAsync_Test_Counters_CountCopiesApartFromPages()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.Create()
            .WriteFile("robots.txt", "User-agent: *")
            .WriteFile("blog/notice.txt", "Archived.");

        // Act
        BuildReport report = await fixture.Builder.BuildAsync(CancellationToken.None);
        int pages = report.Pages.Count;
        int total = pages + report.Copies.Count;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(pages).IsEqualTo(6);
            await Assert.That(total).IsEqualTo(8);

            await Assert.That(Count(fixture, EventIds.SCAN_COMPLETED, "ItemCount")).IsEqualTo(total);
            await Assert.That(Count(fixture, EventIds.RENDER_COMPLETED, "PageCount")).IsEqualTo(pages);
            await Assert.That(Count(fixture, EventIds.COMPOSE_COMPLETED, "PageCount")).IsEqualTo(pages);
            await Assert.That(Count(fixture, EventIds.WRITE_COMPLETED, "FileCount")).IsEqualTo(total);
            await Assert.That(Count(fixture, EventIds.REMOVE_COMPLETED, "FileCount")).IsEqualTo(0);
            await Assert.That(Count(fixture, EventIds.BUILD_COMPLETED, "FileCount")).IsEqualTo(total);
        }
    }

    /// <summary>
    ///     Verifies that the write counter reports what was produced while the remove counter reports what
    ///     was deleted, and that the build counter spans both, which is the total the command prints.
    /// </summary>
    [Test]
    public async Task BuildAsync_Test_Counters_CountRemovalsApartFromWrites()
    {
        // Arrange
        var fixture = ContentFixture.Create();
        await fixture.Builder.BuildAsync(CancellationToken.None);

        fixture.Delete("about.md");
        fixture.Delete("blog/hello.md");
        fixture.Logger.Collector.Clear();

        // Act
        BuildReport report = await fixture.Builder.BuildAsync(CancellationToken.None);
        int written = report.Pages.Count;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(written).IsEqualTo(4);
            await Assert.That(report.Removed.Count).IsEqualTo(2);

            await Assert.That(Count(fixture, EventIds.WRITE_COMPLETED, "FileCount")).IsEqualTo(4);
            await Assert.That(Count(fixture, EventIds.REMOVE_COMPLETED, "FileCount")).IsEqualTo(2);
            await Assert.That(Count(fixture, EventIds.BUILD_COMPLETED, "FileCount")).IsEqualTo(6);
        }
    }

    /// <summary>
    ///     Verifies that every counter is reported at debug level, so that none of them can be promoted into
    ///     the output of a normal build.
    /// </summary>
    /// <remarks>
    ///     The count is pinned before the loop, because an empty list would satisfy the loop on its own.
    /// </remarks>
    [Test]
    public async Task BuildAsync_Test_Counters_AreReportedAtDebug()
    {
        // Arrange
        var fixture = ContentFixture.Create();

        // Act
        await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(fixture.Records.Count).IsEqualTo(6);

            foreach (FakeLogRecord record in fixture.Records)
                await Assert.That(record.Level).IsEqualTo(LogLevel.Debug);
        }
    }

    /// <summary>
    ///     Verifies that every phase counter is accumulated and none is left at zero, and that the total is
    ///     at least the sum of the phases it spans.
    /// </summary>
    /// <remarks>
    ///     Does not verify that the total excludes the cost of reporting the other counters, which is why the
    ///     timestamp for it is taken early: <see cref="FakeLogger{T}"/> writes nothing to a console,
    ///     so that regression only shows up against a real sink.
    /// </remarks>
    [Test]
    public async Task BuildAsync_Test_Counters_AccountForEveryPhase()
    {
        // Arrange
        var fixture = ContentFixture.Create();

        // Act
        await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        double scan = Elapsed(fixture, EventIds.SCAN_COMPLETED);
        double render = Elapsed(fixture, EventIds.RENDER_COMPLETED);
        double compose = Elapsed(fixture, EventIds.COMPOSE_COMPLETED);
        double write = Elapsed(fixture, EventIds.WRITE_COMPLETED);
        double remove = Elapsed(fixture, EventIds.REMOVE_COMPLETED);

        using (Assert.Multiple())
        {
            await Assert.That(scan).IsGreaterThan(0);
            await Assert.That(render).IsGreaterThan(0);
            await Assert.That(compose).IsGreaterThan(0);
            await Assert.That(write).IsGreaterThan(0);

            // Nonzero even on this tree, because the reconcile walks the whole output whether it deletes or not.
            await Assert.That(remove).IsGreaterThan(0);

            await Assert.That(Elapsed(fixture, EventIds.BUILD_COMPLETED))
                .IsGreaterThanOrEqualTo(scan + render + compose + write + remove);
        }
    }

    /// <summary>
    ///     Verifies that the elapsed time the report carries is the same measurement the build counter
    ///     reports, so that what the command prints and what the counter says cannot drift apart.
    /// </summary>
    [Test]
    public async Task BuildAsync_Test_Elapsed_IsTheMeasurementTheBuildCounterReports()
    {
        // Arrange
        var fixture = ContentFixture.Create();

        // Act
        BuildReport report = await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(report.Elapsed).IsGreaterThan(TimeSpan.Zero);

            await Assert.That(report.Elapsed.TotalMilliseconds)
                .IsEqualTo(Elapsed(fixture, EventIds.BUILD_COMPLETED));
        }
    }

    #endregion // Counter Tests

    #region Helpers

    private static IEnumerable<FakeLogRecord> Reports(ContentFixture fixture, int eventId) =>
        fixture.Records.Where(record => record.Id.Id == eventId);

    private static FakeLogRecord Reported(ContentFixture fixture, int eventId) =>
        Reports(fixture, eventId).Single();

    // FakeLogger stringifies the state, so the value is parsed back under the culture it was rendered with.
    private static T Value<T>(FakeLogRecord record, string name) where T : IParsable<T> =>
        T.Parse(record.GetStructuredStateValue(name)!, CultureInfo.InvariantCulture);

    private static int Count(ContentFixture fixture, int eventId, string name) =>
        Value<int>(Reported(fixture, eventId), name);

    private static double Elapsed(ContentFixture fixture, int eventId) =>
        Value<double>(Reported(fixture, eventId), "ElapsedMilliseconds");

    private static IEnumerable<FakeLogRecord> Warnings(ContentFixture fixture) =>
        fixture.Records.Where(static record => record.Level >= LogLevel.Warning);

    private static string ReadFeed(ContentFixture fixture) =>
        fixture.ReadOutput(fixture.FileSystem.Path.Combine(fixture.OutputRoot, "feed.xml"));

    private static IEnumerable<XElement> FeedEntries(ContentFixture fixture) =>
        XElement.Parse(ReadFeed(fixture)).Elements(Atom + "entry");

    // Joined instead of returned as a sequence, because IsEquivalentTo disregards the order under test.
    private static string EntryTitles(ContentFixture fixture) =>
        string.Join(',', FeedEntries(fixture).Select(entry => entry.Element(Atom + "title")?.Value));

    private static string EntryLinks(ContentFixture fixture) =>
        string.Join(',', FeedEntries(fixture).Select(entry => entry.Element(Atom + "id")?.Value));

    private static string ReadPage(ContentFixture fixture, string outputPath) =>
        fixture.ReadOutput(fixture.FileSystem.Path.Combine(fixture.OutputRoot, outputPath));

    private static string ReadFooter(ContentFixture fixture, string outputPath) =>
        FooterBlock().Match(ReadPage(fixture, outputPath)).Value;

    private static string ReadTable(ContentFixture fixture, string outputPath) =>
        TableBlock().Match(ReadPage(fixture, outputPath)).Value;

    private static string TableLinks(ContentFixture fixture, string outputPath) =>
        string.Join(',', TableLink().Matches(ReadTable(fixture, outputPath)).Select(match => match.Groups["href"].Value));

    private static string TableTypes(ContentFixture fixture, string outputPath) =>
        string.Join(',', TableRow().Matches(ReadTable(fixture, outputPath)).Select(match => match.Groups["type"].Value));

    private static string? ReadCanonical(ContentFixture fixture, string outputPath) =>
        Canonical(ReadPage(fixture, outputPath));

    private static string? Canonical(string html)
    {
        Match match = CanonicalLink().Match(html);

        return match.Success ? match.Groups["href"].Value : null;
    }

    private static string? CurrentEntry(ContentFixture fixture, string outputPath)
    {
        Match match = CurrentLink().Match(ReadPage(fixture, outputPath));

        return match.Success ? match.Groups["href"].Value : null;
    }

    // Joined instead of returned as a sequence, because IsEquivalentTo disregards the order under test.
    private static string ReadStylesheets(ContentFixture fixture, string outputPath) =>
        string.Join(',', StylesheetLink().Matches(ReadPage(fixture, outputPath)).Select(match => match.Groups["href"].Value));

    private static string ReadScripts(ContentFixture fixture, string outputPath) =>
        string.Join(',', ScriptTag().Matches(ReadPage(fixture, outputPath)).Select(match => match.Groups["src"].Value));

    // Matched across both tag shapes, because ScriptTag passes over the auto-render tag's onload attribute.
    private static string ReadMathAssets(ContentFixture fixture, string outputPath) =>
        string.Join(',', MathAsset().Matches(ReadPage(fixture, outputPath)).Select(match => match.Groups["url"].Value));

    private static string? ReadDiagramImport(ContentFixture fixture, string outputPath)
    {
        Match match = DiagramImport().Match(ReadPage(fixture, outputPath));

        return match.Success ? match.Groups["url"].Value : null;
    }

    private static string Package(string name, string version) =>
        $"https://cdn.jsdelivr.net/npm/{name}@{version}/dist";

    [GeneratedRegex("""<link rel="canonical" href="(?<href>[^"]*)">""")]
    private static partial Regex CanonicalLink();

    [GeneratedRegex("""<script defer src="(?<src>[^"]*)"></script>""")]
    private static partial Regex ScriptTag();

    [GeneratedRegex("""<link rel="stylesheet" href="(?<href>[^"]*)">""")]
    private static partial Regex StylesheetLink();

    [GeneratedRegex("(?:href|src)=\"(?<url>https://cdn\\.jsdelivr\\.net/npm/katex@[^\"]*)\"")]
    private static partial Regex MathAsset();

    [GeneratedRegex("import mermaid from \"(?<url>[^\"]*)\"")]
    private static partial Regex DiagramImport();

    [GeneratedRegex("aria-current=\"page\" href=\"(?<href>[^\"]*)\"")]
    private static partial Regex CurrentLink();

    [GeneratedRegex("<footer>.*?</footer>", RegexOptions.Singleline)]
    private static partial Regex FooterBlock();

    [GeneratedRegex("<table>.*?</table>", RegexOptions.Singleline)]
    private static partial Regex TableBlock();

    [GeneratedRegex("""<tr data-type="(?<type>[^"]*)">""")]
    private static partial Regex TableRow();

    [GeneratedRegex("""<a href="(?<href>[^"]*)">""")]
    private static partial Regex TableLink();

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
