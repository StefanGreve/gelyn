using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

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
            EventIds.RENDER_COMPLETED,
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
            await Assert.That(Count(fixture, EventIds.RENDER_COMPLETED, "PageCount")).IsEqualTo(files);
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
        double render = Elapsed(fixture, EventIds.RENDER_COMPLETED);
        double write = Elapsed(fixture, EventIds.WRITE_COMPLETED);

        using (Assert.Multiple())
        {
            await Assert.That(walk).IsGreaterThan(0);
            await Assert.That(render).IsGreaterThan(0);
            await Assert.That(write).IsGreaterThan(0);

            await Assert.That(Elapsed(fixture, EventIds.BUILD_COMPLETED))
                .IsGreaterThanOrEqualTo(walk + render + write);
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
