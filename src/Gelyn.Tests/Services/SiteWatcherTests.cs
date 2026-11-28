using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Gelyn.Diagnostics;
using Gelyn.Model.Options;
using Gelyn.Services;
using Gelyn.Tests.Fixtures;

namespace Gelyn.Tests.Services;

/// <summary>
///     Unit tests for the <see cref="SiteWatcher"/> class.
/// </summary>
/// <remarks>
///     Driven over the channel the watchers report into, with the debounce window waited out on a fake clock:
///     the watcher factory of the mock file system throws from every member, and real saves would leave every
///     case below to the timing of the machine it ran on.
/// </remarks>
[Category(Categories.Internal)]
public class SiteWatcherTests
{
    #region Debounce Tests

    /// <summary>
    ///     Verifies that one change rebuilds the site once, and that nothing further is rebuilt while no
    ///     further change is reported.
    /// </summary>
    [Test]
    public async Task RebuildOnChangeAsync_Test_OneChange_RebuildsOnce()
    {
        // Arrange
        await using var harness = WatchHarness.Start();

        // Act
        harness.Change("about.md");

        await harness.WaitForRebuildsAsync(1);
        await harness.QuietAsync();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(harness.Rebuilds).IsEqualTo(1);
            await Assert.That(Collected(harness)).IsEquivalentTo([1]);
            await Assert.That(harness.Stopped).IsFalse();
        }
    }

    /// <summary>
    ///     Verifies that the changes reported inside one window are collected into a single rebuild, which is
    ///     what keeps a save that raises several events, and a formatter that raises several saves, from
    ///     rebuilding the site once per event.
    /// </summary>
    [Test]
    public async Task RebuildOnChangeAsync_Test_ChangesInsideTheWindow_RebuildOnce()
    {
        // Arrange
        await using var harness = WatchHarness.Start();

        // Act
        harness
            .Change("index.md")
            .Change("about.md")
            .Change("blog/index.md")
            .Change("blog/hello.md")
            .Change("blog/2026/index.md");

        await harness.WaitForRebuildsAsync(1);
        await harness.QuietAsync();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(harness.Rebuilds).IsEqualTo(1);
            await Assert.That(Collected(harness)).IsEquivalentTo([5]);
        }
    }

    /// <summary>
    ///     Verifies that an entry reported without having changed is not rebuilt on, so that a rebuild
    ///     reporting the sources it copied cannot drive the loop forever.
    /// </summary>
    [Test]
    public async Task RebuildOnChangeAsync_Test_UnchangedEntry_IsNotRebuiltOn()
    {
        // Arrange
        await using var harness = WatchHarness.Start();

        harness.Change("about.md");
        await harness.WaitForRebuildsAsync(1);

        // Act
        harness.Echo("about.md");
        await harness.QuietAsync();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(harness.Rebuilds).IsEqualTo(1);
            await Assert.That(harness.Errors).IsEmpty();
        }
    }

    /// <summary>
    ///     Verifies that two changes the window does not span are rebuilt once each, so that the debounce
    ///     delays a rebuild instead of dropping one.
    /// </summary>
    [Test]
    public async Task RebuildOnChangeAsync_Test_ChangesEitherSideOfTheWindow_RebuildTwice()
    {
        // Arrange
        await using var harness = WatchHarness.Start();

        // Act
        harness.Change("about.md");
        await harness.WaitForRebuildsAsync(1);

        harness.Change("about.md");
        await harness.WaitForRebuildsAsync(2);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(harness.Rebuilds).IsEqualTo(2);
            await Assert.That(Collected(harness)).IsEquivalentTo([1, 1]);
        }
    }

    /// <summary>
    ///     Verifies that the same entry reported several times inside one window is counted once, which is
    ///     what one save looks like: a write raises a creation and a change for the one file it touched.
    /// </summary>
    [Test]
    public async Task RebuildOnChangeAsync_Test_RepeatedChange_IsCountedOnce()
    {
        // Arrange
        await using var harness = WatchHarness.Start();

        // Act
        harness
            .Change("about.md")
            .Change("about.md")
            .Change("about.md");

        await harness.WaitForRebuildsAsync(1);
        await harness.QuietAsync();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(harness.Rebuilds).IsEqualTo(1);
            await Assert.That(Collected(harness)).IsEquivalentTo([1]);
        }
    }

    #endregion // Debounce Tests

    #region Rebuild Tests

    /// <summary>
    ///     Verifies that the rebuild publishes what the source now says, which is what a reader reloading the
    ///     browser is waiting for.
    /// </summary>
    [Test]
    public async Task RebuildOnChangeAsync_Test_Rebuild_PublishesTheEditedSource()
    {
        // Arrange
        var content = ContentFixture.Create();
        await using var harness = WatchHarness.Start(content);

        // Act
        content.Write("about.md", "Revised");
        harness.Change("about.md");

        await harness.WaitForRebuildsAsync(1);

        // Assert
        await Assert.That(content.ReadOutput(Path.Combine(content.OutputRoot, "about.html"))).Contains("Revised");
    }

    /// <summary>
    ///     Verifies that a rebuild reports one line naming what changed, a summary of the pass. The line per
    ///     file that <c>gelyn build</c> writes would turn every save into noise.
    /// </summary>
    [Test]
    public async Task RebuildOnChangeAsync_Test_Rebuild_ReportsOneSummaryLine()
    {
        // Arrange
        await using var harness = WatchHarness.Start();
        string source = Path.Combine("content", "about.md");

        // Act
        harness.Change("about.md");

        await harness.WaitForLinesAsync(1);
        await harness.QuietAsync();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(harness.Lines.Length).IsEqualTo(1);
            await Assert.That(harness.Lines[0]).StartsWith("rebuilt 6 file(s) in ");
            await Assert.That(harness.Lines[0]).EndsWith($"ms after {source} changed.");
        }
    }

    /// <summary>
    ///     Verifies that a window holding more than one change reports their number and names none of
    ///     them as though it were the only one.
    /// </summary>
    [Test]
    public async Task RebuildOnChangeAsync_Test_SeveralChanges_AreReportedByTheirNumber()
    {
        // Arrange
        await using var harness = WatchHarness.Start();

        // Act
        harness.Change("about.md").Change("index.md");

        await harness.WaitForLinesAsync(1);

        // Assert
        await Assert.That(harness.Lines[0]).EndsWith("ms after 2 entries changed.");
    }

    /// <summary>
    ///     Verifies that a rebuild which throws is reported and leaves the loop running, and that the next
    ///     change is still rebuilt. A half-saved file that does not parse is the normal case in an editor, and
    ///     a preview server that exits on it is unusable.
    /// </summary>
    [Test]
    public async Task RebuildOnChangeAsync_Test_FailedRebuild_LeavesTheLoopRunning()
    {
        // Arrange
        var content = ContentFixture.Create();
        await using var harness = WatchHarness.Start(content);

        // Act
        content.Delete("index.md");
        harness.Change("index.md");

        await harness.WaitForErrorsAsync(1);

        content.Write("index.md", "Home");
        harness.Change("index.md");

        await harness.WaitForRebuildsAsync(1);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(harness.Errors.Length).IsEqualTo(1);
            await Assert.That(harness.Errors[0]).Contains("No landing page");
            await Assert.That(harness.Rebuilds).IsEqualTo(1);
            await Assert.That(harness.Stopped).IsFalse();
        }
    }

    /// <summary>
    ///     Verifies that a page saved with malformed front matter is reported by one error that names the file and
    ///     the line, that the loop keeps running, and that the next save is rebuilt.
    /// </summary>
    [Test]
    public async Task RebuildOnChangeAsync_Test_MalformedFrontMatter_LeavesTheLoopRunning()
    {
        // Arrange
        var content = ContentFixture.Create();
        await using var harness = WatchHarness.Start(content);

        // Act
        content.WriteFile("about.md", "---\ntitle: \"About\n---\n\nAbout");
        harness.Change("about.md");

        await harness.WaitForErrorsAsync(1);

        content.WriteFile("about.md", "---\ntitle: \"About\"\n---\n\nAbout");
        harness.Change("about.md");

        await harness.WaitForRebuildsAsync(1);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(harness.Errors.Length).IsEqualTo(1);
            await Assert.That(harness.Errors[0]).Contains("about.md").And.Contains("is malformed on line 2");
            await Assert.That(harness.Rebuilds).IsEqualTo(1);
            await Assert.That(harness.Stopped).IsFalse();
        }
    }

    #endregion // Rebuild Tests

    #region Configuration Tests

    /// <summary>
    ///     Verifies that a change to the configuration file is read back before the rebuild it triggers,
    ///     because a rebuild against the options the monitor has cached would publish the values the file no
    ///     longer holds.
    /// </summary>
    [Test]
    public async Task RebuildOnChangeAsync_Test_ConfigurationChange_IsReloadedBeforeTheRebuild()
    {
        // Arrange
        await using var harness = WatchHarness.Start();

        int[] expected =
        [
            EventIds.CHANGE_DETECTED,
            EventIds.CONFIGURATION_RELOADED,
            EventIds.REBUILD_COMPLETED,
        ];

        // Act
        harness.ChangeConfiguration();

        await harness.WaitForRebuildsAsync(1);

        // Assert
        string reported = string.Join(',', harness.Records.Select(static record => record.Id.Id));

        await Assert.That(reported).IsEqualTo(string.Join(',', expected));
    }

    /// <summary>
    ///     Verifies that the configuration file is recognized among the changes whatever casing it is
    ///     reported under, which is what a file system that resolves a path without regard to case hands the
    ///     watcher.
    /// </summary>
    [Test]
    public async Task RebuildOnChangeAsync_Test_ConfigurationChange_IsRecognizedWhateverItsCasing()
    {
        // Arrange
        await using var harness = WatchHarness.Start();

        // Act
        harness.ChangeConfiguration(harness.ConfigurationPath.ToUpperInvariant());

        await harness.WaitForRebuildsAsync(1);

        // Assert
        await Assert.That(harness.Reloads).IsEqualTo(1);
    }

    /// <summary>
    ///     Verifies that a change to a source reloads nothing, so that the file provider is read again when
    ///     the file behind it changed and at no other time.
    /// </summary>
    [Test]
    public async Task RebuildOnChangeAsync_Test_SourceChange_ReloadsNothing()
    {
        // Arrange
        await using var harness = WatchHarness.Start();

        // Act
        harness.Change("about.md");

        await harness.WaitForRebuildsAsync(1);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(harness.Reloads).IsEqualTo(0);
            await Assert.That(harness.Rebuilds).IsEqualTo(1);
        }
    }

    /// <summary>
    ///     Verifies that a reload the settings cannot complete is reported by what it said, leaves the loop
    ///     running on the settings it last loaded, and leaves the next change still rebuilt. The cases are the
    ///     failures a file under the cursor produces, which are a file that does not parse, a value the binder
    ///     cannot convert, and a value the validator rejects.
    /// </summary>
    [Test]
    [Arguments("""{ "Title": """, "Failed to load configuration from file")]
    [Arguments("""{ "Port": "soon" }""", "Failed to convert configuration value")]
    [Arguments("""{ "Title": "" }""", "Title must not be empty.")]
    public async Task RebuildOnChangeAsync_Test_RejectedReload_LeavesTheLoopRunning(string contents, string message)
    {
        // Arrange
        await using var harness = WatchHarness.Start();

        // Act
        harness.WriteConfiguration(contents).ChangeConfiguration();
        await harness.WaitForErrorsAsync(1);

        harness.Change("about.md");
        await harness.WaitForRebuildsAsync(1);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(harness.Errors.Length).IsEqualTo(1);
            await Assert.That(harness.ErrorOutput).Contains(message).And.DoesNotContain("One or more errors");
            await Assert.That(harness.Reloads).IsEqualTo(0);
            await Assert.That(harness.Rebuilds).IsEqualTo(1);
            await Assert.That(harness.Stopped).IsFalse();
        }
    }

    /// <summary>
    ///     Verifies that a directory setting the session cannot follow is reported and rebuilds nothing: both
    ///     settings moved would leave the watchers on one tree and the server on a directory the rebuild no
    ///     longer writes to.
    /// </summary>
    [Test]
    [Arguments(nameof(SiteOptions.ContentDirectory))]
    [Arguments(nameof(SiteOptions.OutputDirectory))]
    public async Task RebuildOnChangeAsync_Test_MovedDirectory_IsReportedWithoutARebuild(string setting)
    {
        // Arrange
        var content = ContentFixture.Create();
        await using var harness = WatchHarness.Start(content);

        // Act
        string moved = setting.Equals(nameof(SiteOptions.ContentDirectory), StringComparison.Ordinal)
            ? """{ "ContentDirectory": "pages" }"""
            : """{ "OutputDirectory": "public" }""";

        harness.WriteConfiguration(moved).ChangeConfiguration();

        await harness.WaitForWarningsAsync(1);
        await harness.QuietAsync();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(harness.Warnings.Length).IsEqualTo(1);
            await Assert.That(harness.ErrorOutput).Contains($"{setting} changed.").And.Contains("Restart gelyn serve");
            await Assert.That(harness.Rebuilds).IsEqualTo(0);
            await Assert.That(harness.Stopped).IsFalse();
        }
    }

    /// <summary>
    ///     Verifies that a base URL whose path moved is reported and rebuilds nothing, because the server
    ///     keeps answering under the path it started with, while a change to the origin alone rebuilds.
    /// </summary>
    [Test]
    [Arguments(null, "https://example.com/docs/", true)]
    [Arguments("https://example.com/docs", "https://www.example.com/docs/", false)]
    public async Task RebuildOnChangeAsync_Test_MovedBasePath_IsReportedWithoutARebuild(string? started, string changed, bool isMoved)
    {
        // Arrange
        var content = ContentFixture.Create();
        content.Options.BaseUrl = started is null ? null : new Uri(started);

        await using var harness = WatchHarness.Start(content);

        // Act
        harness.WriteConfiguration($$"""{ "BaseUrl": "{{changed}}" }""").ChangeConfiguration();

        if (isMoved)
            await harness.WaitForWarningsAsync(1);
        else
            await harness.WaitForRebuildsAsync(1);

        await harness.QuietAsync();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(harness.Warnings.Length).IsEqualTo(isMoved ? 1 : 0);
            await Assert.That(harness.Rebuilds).IsEqualTo(isMoved ? 0 : 1);
        }

        if (isMoved)
            await Assert.That(harness.ErrorOutput).Contains($"{nameof(SiteOptions.BaseUrl)} changed.");
    }

    #endregion // Configuration Tests

    #region Cancellation Tests

    /// <summary>
    ///     Verifies that canceling stops the loop, which is how <c>gelyn serve</c> ends and therefore not a
    ///     failure.
    /// </summary>
    [Test]
    public async Task RebuildOnChangeAsync_Test_Cancellation_StopsTheLoop()
    {
        // Arrange
        await using var harness = WatchHarness.Start();

        // Act
        await harness.StopAsync();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(harness.Stopped).IsTrue();
            await Assert.That(harness.Rebuilds).IsEqualTo(0);
        }
    }

    /// <summary>
    ///     Verifies that a change reported after the loop has stopped rebuilds nothing, so that a rebuild
    ///     cannot outlive the session it belongs to.
    /// </summary>
    [Test]
    public async Task RebuildOnChangeAsync_Test_ChangeAfterCancellation_RebuildsNothing()
    {
        // Arrange
        await using var harness = WatchHarness.Start();
        await harness.StopAsync();

        // Act
        harness.Change("about.md");

        await harness.QuietAsync();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(harness.Rebuilds).IsEqualTo(0);
            await Assert.That(harness.Lines).IsEmpty();
        }
    }

    #endregion // Cancellation Tests

    #region Helpers

    // FakeLogger stringifies the state, so the count is parsed back under the culture it was rendered with.
    private static int[] Collected(WatchHarness harness) =>
    [
        .. harness.Records
            .Where(static record => record.Id.Id == EventIds.CHANGE_DETECTED)
            .Select(static record => int.Parse(record.GetStructuredStateValue("ChangeCount")!, CultureInfo.InvariantCulture)),
    ];

    #endregion // Helpers
}
