using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Gelyn.Core.Internals;
using Gelyn.Services;
using Gelyn.Tests.Stubs;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

using Spectre.Console;

namespace Gelyn.Tests.Fixtures;

/// <summary>
///     A <see cref="SiteWatcher"/> whose rebuild loop is running over an in-memory content tree, together
///     with the channel a change is reported through and the clock the debounce window is waited out on.
/// </summary>
/// <remarks>
///     No <see cref="FileSystemWatcher"/> is involved: a test writes the paths one would have reported into
///     the channel itself, which is what makes the four debounce cases exact instead of a matter of real
///     saves and real sleeps. Disposal cancels the loop the way Ctrl+C does and waits for it, so a loop that
///     never stops fails the test that started it and does not leak into the next one.
/// </remarks>
internal sealed class WatchHarness : IAsyncDisposable
{
    private const int SettleTimeoutMilliseconds = 10_000;
    private const int PollMilliseconds = 5;
    private const int QuietSteps = 10;

    /// <summary>
    ///     How far one step moves the clock, which is well past the debounce window the loop waits out.
    /// </summary>
    /// <remarks>
    ///     Advanced in steps and never in one jump, because a timer the loop registers after an advance starts
    ///     from the clock as that advance left it and needs a further one to fire at all.
    /// </remarks>
    private static readonly TimeSpan AdvanceStep = TimeSpan.FromSeconds(1);

    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _watching;

    private readonly StringWriter _output = new();
    private readonly TextWriter _writer;

    private readonly StringWriter _errorOutput = new();
    private readonly TextWriter _errorWriter;

    private WatchHarness(ContentFixture content, IConfigurationRoot configuration)
    {
        this.Content = content;
        this.Logger = new FakeLogger<SiteWatcher>();
        this.TimeProvider = new FakeTimeProvider();
        this.Configuration = configuration;

        this.ContentDirectory = PathBoundary.Resolve(content.Root, content.Options.ContentDirectory);
        this.OutputDirectory = PathBoundary.Resolve(content.Root, content.Options.OutputDirectory);
        this.ConfigurationPath = PathBoundary.Resolve(content.Root, ConfigurationFile.DefaultFileName);

        // Synchronized because the loop appends while a test reads, which a bare StringWriter does not order.
        this._writer = TextWriter.Synchronized(this._output);
        this._errorWriter = TextWriter.Synchronized(this._errorOutput);

        // The two streams are captured apart, so a test can tell which one a message reached.
        IAnsiConsole stdOut = CapturedConsole.Create(this._writer);
        IAnsiConsole stdErr = CapturedConsole.Create(this._errorWriter);

        this.Watcher = new SiteWatcher(
            content.Builder,
            this.Configuration,
            content.HostEnvironment,
            new OptionsMonitorStub(content.Options),
            stdOut,
            stdErr,
            this.TimeProvider,
            content.FileSystem,
            this.Logger);

        this._watching = this.Watcher.RebuildOnChangeAsync(
            this.ContentDirectory,
            this.OutputDirectory,
            this.ConfigurationPath,
            this._cancellation.Token);
    }

    /// <summary>
    ///     The content tree the loop rebuilds, and the builder it rebuilds it with.
    /// </summary>
    public ContentFixture Content { get; }

    /// <summary>
    ///     The watcher under test.
    /// </summary>
    public SiteWatcher Watcher { get; }

    /// <summary>
    ///     The configuration the loop reloads when the configuration file is what changed.
    /// </summary>
    public IConfigurationRoot Configuration { get; }

    /// <summary>
    ///     The clock the debounce window is waited out on, which moves only when a test advances it.
    /// </summary>
    public FakeTimeProvider TimeProvider { get; }

    /// <summary>
    ///     The records the loop has written.
    /// </summary>
    public FakeLogger<SiteWatcher> Logger { get; }

    /// <summary>
    ///     The absolute path of the watched content directory, as the loop was started against it.
    /// </summary>
    public string ContentDirectory { get; }

    /// <summary>
    ///     The absolute path of the served output directory, as the loop was started against it.
    /// </summary>
    public string OutputDirectory { get; }

    /// <summary>
    ///     The absolute path of the configuration file the loop reloads on.
    /// </summary>
    public string ConfigurationPath { get; }

    /// <summary>
    ///     The records the loop has written, in the order it wrote them.
    /// </summary>
    public IReadOnlyList<FakeLogRecord> Records =>
        this.Logger.Collector.GetSnapshot();

    /// <summary>
    ///     Everything the loop has written to its console.
    /// </summary>
    public string Output
    {
        get
        {
            // A synchronized writer locks on itself, so the same lock here orders a read against a write.
            lock (this._writer)
            {
                return this._output.ToString();
            }
        }
    }

    /// <summary>
    ///     What the loop has written, one element per line. A line the console wrapped is therefore more
    ///     than one element, which is why an assertion about wording reads <see cref="Output"/> instead.
    /// </summary>
    public string[] Lines =>
        this.Output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    ///     The number of rebuilds the loop has completed.
    /// </summary>
    public int Rebuilds =>
        this.Count(EventIds.REBUILD_COMPLETED);

    /// <summary>
    ///     The number of configuration reloads the loop has performed.
    /// </summary>
    public int Reloads =>
        this.Count(EventIds.CONFIGURATION_RELOADED);

    /// <summary>
    ///     Everything the loop has written to standard error.
    /// </summary>
    public string ErrorOutput
    {
        get
        {
            lock (this._errorWriter)
            {
                return this._errorOutput.ToString();
            }
        }
    }

    /// <summary>
    ///     What the loop has written to standard error, one element per line.
    /// </summary>
    public string[] ErrorLines =>
        this.ErrorOutput.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    ///     The lines reporting a rebuild that failed. Read off standard error, so a count also pins the
    ///     stream the report reached.
    /// </summary>
    public string[] Errors =>
        [.. this.ErrorLines.Where(static line => line.StartsWith("error:", StringComparison.Ordinal))];

    /// <summary>
    ///     The lines reporting something the session cannot follow, read off standard error for the reason
    ///     <see cref="Errors"/> gives.
    /// </summary>
    public string[] Warnings =>
        [.. this.ErrorLines.Where(static line => line.StartsWith("warning:", StringComparison.Ordinal))];

    /// <summary>
    ///     Indicates whether the loop has stopped.
    /// </summary>
    public bool Stopped =>
        this._watching.IsCompleted;

    /// <summary>
    ///     Starts a rebuild loop over one content tree.
    /// </summary>
    /// <param name="content">
    ///     The tree to rebuild, which defaults to the fixture's own.
    /// </param>
    /// <param name="configuration">
    ///     The configuration the loop reloads, which defaults to one that holds nothing and reloads without
    ///     complaint. What a test observes of a reload is that the loop asked for it, and not what the
    ///     reload read: the options a rebuild is driven by come from the monitor the fixture supplies.
    /// </param>
    /// <returns>
    ///     The new harness.
    /// </returns>
    public static WatchHarness Start(ContentFixture? content = null, IConfigurationRoot? configuration = null) =>
        new(content ?? ContentFixture.Create(), configuration ?? new ConfigurationRootStub());

    /// <summary>
    ///     Reports one change under the content directory, as the content watcher would have.
    /// </summary>
    /// <param name="relativePath">
    ///     The path of the entry relative to the content directory, separated by forward slashes.
    /// </param>
    /// <returns>
    ///     The same harness, so that calls can be chained.
    /// </returns>
    public WatchHarness Change(string relativePath)
    {
        this.Report(Path.Combine(
            this.ContentDirectory,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));

        return this;
    }

    /// <summary>
    ///     Reports one entry under the content directory without changing it, as a rebuild copying the
    ///     sources it just read makes the content watcher do.
    /// </summary>
    /// <param name="relativePath">
    ///     The path of the entry relative to the content directory, separated by forward slashes.
    /// </param>
    /// <returns>
    ///     The same harness, so that calls can be chained.
    /// </returns>
    public WatchHarness Echo(string relativePath)
    {
        this.Watcher.Changes.TryWrite(Path.Combine(
            this.ContentDirectory,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));

        return this;
    }

    /// <summary>
    ///     Reports a change to the configuration file, as the watcher over it would have.
    /// </summary>
    /// <param name="path">
    ///     The spelling to report it under, which defaults to the path the loop was started against.
    /// </param>
    /// <returns>
    ///     The same harness, so that calls can be chained.
    /// </returns>
    public WatchHarness ChangeConfiguration(string? path = null)
    {
        this.Report(path ?? this.ConfigurationPath);

        return this;
    }

    /// <summary>
    ///     Advances the clock until the loop has completed a given number of rebuilds.
    /// </summary>
    /// <param name="count">
    ///     The number of rebuilds to wait for.
    /// </param>
    /// <returns>
    ///     A task that completes once that many rebuilds have been reported.
    /// </returns>
    /// <exception cref="TimeoutException">
    ///     The rebuilds did not complete in the time an in-memory build needs many times over.
    /// </exception>
    public Task WaitForRebuildsAsync(int count) =>
        this.AdvanceUntilAsync(() => this.Rebuilds >= count, $"{count} rebuild(s)");

    /// <summary>
    ///     Advances the clock until the loop has written a given number of lines.
    /// </summary>
    /// <remarks>
    ///     The loop logs that a rebuild completed before it writes the line reporting it, so a test that waits
    ///     on <see cref="WaitForRebuildsAsync(int)"/> and then reads <see cref="Lines"/> can reach the
    ///     assertion while the line is still unwritten. A test asserting on what was written waits here.
    /// </remarks>
    /// <param name="count">
    ///     The number of lines to wait for.
    /// </param>
    /// <returns>
    ///     A task that completes once that many lines have been written.
    /// </returns>
    /// <exception cref="TimeoutException">
    ///     The lines did not arrive in the time an in-memory build needs many times over.
    /// </exception>
    public Task WaitForLinesAsync(int count) =>
        this.AdvanceUntilAsync(() => this.Lines.Length >= count, $"{count} line(s)");

    /// <summary>
    ///     Advances the clock until the loop has reported a given number of failed rebuilds.
    /// </summary>
    /// <param name="count">
    ///     The number of failures to wait for.
    /// </param>
    /// <returns>
    ///     A task that completes once that many failures have been reported.
    /// </returns>
    /// <exception cref="TimeoutException">
    ///     The failures did not arrive in the time an in-memory build needs many times over.
    /// </exception>
    public Task WaitForErrorsAsync(int count) =>
        this.AdvanceUntilAsync(() => this.Errors.Length >= count, $"{count} failed rebuild(s)");

    /// <summary>
    ///     Advances the clock until the loop has reported a given number of changes it cannot follow.
    /// </summary>
    /// <param name="count">
    ///     The number of warnings to wait for.
    /// </param>
    /// <returns>
    ///     A task that completes once that many warnings have been reported.
    /// </returns>
    /// <exception cref="TimeoutException">
    ///     The warnings did not arrive in the time an in-memory build needs many times over.
    /// </exception>
    public Task WaitForWarningsAsync(int count) =>
        this.AdvanceUntilAsync(() => this.Warnings.Length >= count, $"{count} warning(s)");

    /// <summary>
    ///     Advances the clock well past the debounce window several times over, so that a test can show that
    ///     nothing further was rebuilt.
    /// </summary>
    /// <remarks>
    ///     The only way to observe the absence of a rebuild is to leave the loop room to perform one, which
    ///     is what the real delay between the steps is for.
    /// </remarks>
    /// <returns>
    ///     A task that completes once the clock has been advanced that often.
    /// </returns>
    public async Task QuietAsync()
    {
        for (int step = 0; step < QuietSteps; step++)
        {
            this.TimeProvider.Advance(AdvanceStep);

            await Task.Delay(PollMilliseconds).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Cancels the loop and waits for it to stop, as <see cref="DisposeAsync"/> does, so that a test can
    ///     assert what cancelling does.
    /// </summary>
    /// <returns>
    ///     A task that completes once the loop has stopped.
    /// </returns>
    public async Task StopAsync()
    {
        await this._cancellation.CancelAsync().ConfigureAwait(false);
        await this._watching.ConfigureAwait(false);
    }

    /// <summary>
    ///     Cancels the loop, waits for it to stop, and releases what it wrote to.
    /// </summary>
    /// <returns>
    ///     A task that completes once the loop has stopped.
    /// </returns>
    public async ValueTask DisposeAsync()
    {
        await this.StopAsync().ConfigureAwait(false);

        this._cancellation.Dispose();
        this._output.Dispose();
    }

    #region Helpers

    /// <summary>
    ///     Writes one path into the channel the way a watcher would, having first made the entry behind it
    ///     look changed.
    /// </summary>
    /// <remarks>
    ///     The loop ignores a reported path whose write time and length match what the last rebuild found
    ///     there, which is how it tells a save apart from the echo of the rebuild it triggered, so reporting
    ///     an entry without moving its write time reports nothing at all. A path with no file behind it is
    ///     left as is, since the loop rebuilds on one regardless.
    /// </remarks>
    /// <param name="path">
    ///     The absolute path to report.
    /// </param>
    private void Report(string path)
    {
        if (this.Content.FileSystem.File.Exists(path))
        {
            this.Content.FileSystem.File.SetLastWriteTimeUtc(
                path,
                this.TimeProvider.GetUtcNow().UtcDateTime);
        }

        this.Watcher.Changes.TryWrite(path);
    }

    private int Count(int eventId) =>
        this.Records.Count(record => record.Id.Id == eventId);

    private async Task AdvanceUntilAsync(Func<bool> reached, string expectation)
    {
        long deadline = Environment.TickCount64 + SettleTimeoutMilliseconds;

        while (!reached())
        {
            if (Environment.TickCount64 > deadline)
                throw new TimeoutException($"Waited for {expectation}, read {this.Rebuilds} rebuild(s).");

            this.TimeProvider.Advance(AdvanceStep);

            await Task.Delay(PollMilliseconds).ConfigureAwait(false);
        }
    }

    #endregion // Helpers
}
