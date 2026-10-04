using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Gelyn.Components;
using Gelyn.Internals;
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
        BuildReport report = await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(report.Files.Count).IsEqualTo(6);
            await Assert.That(report.Files[0]).IsEqualTo("index.html");
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

    #region Shared Fragment Tests

    /// <summary>
    ///     Verifies that every page marks its own navigation entry as current and no other, and that a page the
    ///     navigation leaves out marks none at all.
    /// </summary>
    /// <remarks>
    ///     The banner is rendered once per variant rather than once per page, so this is what prevents one
    ///     page's current entry from being served to the rest of the site.
    /// </remarks>
    [Test]
    public async Task BuildAsync_Test_Header_MarksTheCurrentPageOnly()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.Create();
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
    ///     Verifies that every page carries the same footer, which is the invariant that makes rendering it
    ///     once for the whole build correct.
    /// </summary>
    [Test]
    public async Task BuildAsync_Test_Footer_IsIdenticalOnEveryPage()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.Create();

        // Act
        await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        string[] footers =
        [
            .. fixture.EnumerateOutput().Select(page => FooterBlock().Match(fixture.ReadOutput(page)).Value),
        ];

        using (Assert.Multiple())
        {
            await Assert.That(footers.Length).IsEqualTo(6);
            await Assert.That(footers.Distinct().Count()).IsEqualTo(1);
            await Assert.That(footers[0]).Contains("Built with Gelyn");
        }
    }

    /// <summary>
    ///     Verifies that rendering the footer against any page produces the same markup, which is the invariant
    ///     that licenses rendering it once for the whole build.
    /// </summary>
    /// <remarks>
    ///     Renders the component directly, because comparing the footers in the output cannot detect this:
    ///     a footer that did read the page would be rendered once from one page and then reused, so every page
    ///     would still carry identical markup.
    /// </remarks>
    [Test]
    public async Task BuildAsync_Test_Footer_DoesNotDependOnThePage()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.Create();

        IReadOnlyList<ContentPage> pages =
            await fixture.Pipeline.WalkAsync(fixture.Options, CancellationToken.None);

        IReadOnlyList<ContentPage> navigation = [.. pages.Where(static page => page.InNavigation)];

        // Act
        string[] footers =
        [
            .. pages.Select(page => FooterComponent.Render(new RenderContext
            {
                Options = fixture.Options,
                Navigation = navigation,
                Page = page,
                GeneratedAt = DateOnly.FromDateTime(ContentFixture.GeneratedAt.UtcDateTime),
            })),
        ];

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(footers.Length).IsEqualTo(6);
            await Assert.That(footers.Distinct().Count()).IsEqualTo(1);
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
        ContentFixture fixture = ContentFixture.Create();

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
    [Test]
    public async Task BuildAsync_Test_Canonical_IsOmittedWithoutABaseUrl()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.Create();

        // Act
        await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert: the count is pinned first, because an empty output would satisfy the loop on its own.
        using (Assert.Multiple())
        {
            await Assert.That(fixture.EnumerateOutput().Count()).IsEqualTo(6);

            foreach (string page in fixture.EnumerateOutput())
                await Assert.That(Canonical(fixture.ReadOutput(page))).IsNull();
        }
    }

    #endregion // Canonical Tests

    #region Counter Tests

    /// <summary>
    ///     Verifies that the build reports one counter per phase, in the order the phases run.
    /// </summary>
    [Test]
    public async Task BuildAsync_Test_Counters_AreReportedInPhaseOrder()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.Create();

        int[] phases =
        [
            EventIds.WALK_COMPLETED,
            EventIds.COMPOSE_COMPLETED,
            EventIds.WRITE_COMPLETED,
            EventIds.BUILD_COMPLETED,
        ];

        // Act
        await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert: compared as a joined string because IsEquivalentTo disregards order.
        string reported = string.Join(',', fixture.Records.Select(static record => record.Id.Id));

        await Assert.That(reported).IsEqualTo(string.Join(',', phases));
    }

    /// <summary>
    ///     Verifies that all four counters report the number of files the build produced, so that none of
    ///     them can drift from the result the command returns.
    /// </summary>
    /// <remarks>
    ///     Cannot tell the page count and the file count apart, because every discovered page is written and
    ///     the two counts are therefore always equal.
    /// </remarks>
    [Test]
    public async Task BuildAsync_Test_Counters_ReportTheNumberOfFiles()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.Create();

        // Act
        BuildReport report = await fixture.Builder.BuildAsync(CancellationToken.None);
        int files = report.Files.Count;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(Count(fixture, EventIds.WALK_COMPLETED, "PageCount")).IsEqualTo(files);
            await Assert.That(Count(fixture, EventIds.COMPOSE_COMPLETED, "PageCount")).IsEqualTo(files);
            await Assert.That(Count(fixture, EventIds.WRITE_COMPLETED, "FileCount")).IsEqualTo(files);
            await Assert.That(Count(fixture, EventIds.BUILD_COMPLETED, "FileCount")).IsEqualTo(files);
        }
    }

    /// <summary>
    ///     Verifies that every counter is reported at debug level, so that none of them can be promoted into
    ///     the output of a normal build.
    /// </summary>
    [Test]
    public async Task BuildAsync_Test_Counters_AreReportedAtDebug()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.Create();

        // Act
        await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert: the count is pinned first, because an empty list would satisfy the loop on its own.
        using (Assert.Multiple())
        {
            await Assert.That(fixture.Records.Count).IsEqualTo(4);

            foreach (FakeLogRecord record in fixture.Records)
                await Assert.That(record.Level).IsEqualTo(LogLevel.Debug);
        }
    }

    /// <summary>
    ///     Verifies that the build reports nothing when the requested level discards debug records, which is
    ///     what proves the level is honored rather than merely configured.
    /// </summary>
    [Test]
    public async Task BuildAsync_Test_Counters_AreSuppressedAboveDebug()
    {
        // Arrange
        ContentFixture fixture = ContentFixture.Create(LogLevel.Warning);

        // Act
        await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        await Assert.That(fixture.Records.Count).IsEqualTo(0);
    }

    /// <summary>
    ///     Verifies that every phase counter is accumulated rather than left at zero, and that the total is
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
        ContentFixture fixture = ContentFixture.Create();

        // Act
        await fixture.Builder.BuildAsync(CancellationToken.None);

        // Assert
        double walk = Elapsed(fixture, EventIds.WALK_COMPLETED);
        double compose = Elapsed(fixture, EventIds.COMPOSE_COMPLETED);
        double write = Elapsed(fixture, EventIds.WRITE_COMPLETED);

        using (Assert.Multiple())
        {
            await Assert.That(walk).IsGreaterThan(0);
            await Assert.That(compose).IsGreaterThan(0);
            await Assert.That(write).IsGreaterThan(0);

            await Assert.That(Elapsed(fixture, EventIds.BUILD_COMPLETED))
                .IsGreaterThanOrEqualTo(walk + compose + write);
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
        ContentFixture fixture = ContentFixture.Create();

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

    private static FakeLogRecord Reported(ContentFixture fixture, int eventId) =>
        fixture.Records.Single(record => record.Id.Id == eventId);

    // FakeLogger captures the state already stringified, so the value has to be parsed back rather than cast.
    // The invariant culture matches what the logger used to render it.
    private static T Value<T>(FakeLogRecord record, string name) where T : IParsable<T> =>
        T.Parse(record.GetStructuredStateValue(name)!, CultureInfo.InvariantCulture);

    private static int Count(ContentFixture fixture, int eventId, string name) =>
        Value<int>(Reported(fixture, eventId), name);

    private static double Elapsed(ContentFixture fixture, int eventId) =>
        Value<double>(Reported(fixture, eventId), "ElapsedMilliseconds");

    private static string ReadPage(ContentFixture fixture, string outputPath) =>
        fixture.ReadOutput(fixture.FileSystem.Path.Combine(fixture.OutputRoot, outputPath));

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

    [GeneratedRegex("""<link rel="canonical" href="(?<href>[^"]*)">""")]
    private static partial Regex CanonicalLink();

    [GeneratedRegex("aria-current=\"page\" href=\"(?<href>[^\"]*)\"")]
    private static partial Regex CurrentLink();

    [GeneratedRegex("<footer>.*?</footer>", RegexOptions.Singleline)]
    private static partial Regex FooterBlock();

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
