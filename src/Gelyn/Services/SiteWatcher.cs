using System.Diagnostics;
using System.IO.Abstractions;
using System.Threading.Channels;

using Gelyn.Abstractions;
using Gelyn.Core.Internals;
using Gelyn.Diagnostics;
using Gelyn.Exceptions;
using Gelyn.Extensions;
using Gelyn.Model;
using Gelyn.Model.Options;

using Microsoft.Extensions.Logging;

using SharpYaml;

using Spectre.Console;

namespace Gelyn.Services;

/// <summary>
///     <inheritdoc path="/summary/node()"/>
/// </summary>
/// <remarks>
///     Two watchers report into one channel the rebuild loop reads, so a rebuild never runs on a callback
///     thread, where a <see cref="FileSystemEventHandler"/> returning <see langword="void"/> would leave a
///     failure unobserved and block the watcher while its buffer fills. The tests drive that channel too.
/// </remarks>
internal sealed class SiteWatcher : SiteWatcherContract
{
    /// <summary>
    ///     How long the loop waits for further changes before it rebuilds.
    /// </summary>
    /// <remarks>
    ///     The burst a save produces, multiplied by any formatter with it, is what the window reduces to one
    ///     rebuild. It opens with the first change and later ones do not extend it.
    /// </remarks>
    private static readonly TimeSpan DebounceWindow = TimeSpan.FromMilliseconds(150);

    /// <summary>
    ///     The changes the watchers have reported and the loop has yet to rebuild on.
    /// </summary>
    private readonly Channel<string> _changes = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
    {
        SingleReader = true,
    });

    /// <summary>
    ///     What the last rebuild found at each path a watcher has reported.
    /// </summary>
    /// <remarks>
    ///     Only the loop reads and writes this, so it needs no synchronization of its own.
    /// </remarks>
    private readonly Dictionary<string, (DateTime Written, long Length)> _entries =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly SiteBuilderContract _builder;
    private readonly Func<SiteOptions> _reload;
    private readonly string _contentRoot;
    private readonly string _configurationFile;
    private SiteOptions _options;
    private readonly IFileSystem _fileSystem;
    private readonly IAnsiConsole _stdOut;
    private readonly IAnsiConsole _stdErr;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SiteWatcher> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SiteWatcher"/> class.
    /// </summary>
    /// <param name="builder">
    ///     Performs the generation each change triggers.
    /// </param>
    /// <param name="options">
    ///     The settings the loop starts from, which name the directories to watch.
    /// </param>
    /// <param name="reload">
    ///     Loads the settings again when the configuration file changes.
    /// </param>
    /// <param name="contentRoot">
    ///     The directory relative paths are resolved against.
    /// </param>
    /// <param name="configurationFile">
    ///     The absolute path of the configuration file, which the loop watches for changes.
    /// </param>
    /// <param name="stdOut">
    ///     The console the command writes its output to.
    /// </param>
    /// <param name="stdErr">
    ///     The console the loop writes its warnings and failures to.
    /// </param>
    /// <param name="timeProvider">
    ///     Supplies the timer the debounce window is waited out on.
    /// </param>
    /// <param name="fileSystem">
    ///     Reads the write time and the length a reported path carries.
    /// </param>
    /// <param name="logger">
    ///     Receives the records the loop writes.
    /// </param>
    public SiteWatcher(
        SiteBuilderContract builder,
        SiteOptions options,
        Func<SiteOptions> reload,
        string contentRoot,
        string configurationFile,
        IAnsiConsole stdOut,
        IAnsiConsole stdErr,
        TimeProvider timeProvider,
        IFileSystem fileSystem,
        ILogger<SiteWatcher> logger)
    {
        this._builder = builder;
        this._options = options;
        this._reload = reload;
        this._contentRoot = contentRoot;
        this._configurationFile = configurationFile;
        this._stdOut = stdOut;
        this._stdErr = stdErr;
        this._timeProvider = timeProvider;
        this._fileSystem = fileSystem;
        this._logger = logger;
    }

    /// <summary>
    ///     The writer the watchers hand their events to, and the seam a test writes a change into.
    /// </summary>
    internal ChannelWriter<string> Changes =>
        this._changes.Writer;

    /// <inheritdoc/>
    public override async Task WatchAsync(CancellationToken cancellationToken)
    {
        SiteOptions options = this._options;
        string root = this._contentRoot;

        string contentDirectory = PathBoundary.Resolve(root, options.ContentDirectory);
        string outputDirectory = PathBoundary.Resolve(root, options.OutputDirectory);
        string basePath = UrlUtils.ResolveBasePath(options.BaseUrl);
        string configurationFile = this._configurationFile;

        using FileSystemWatcher sources = Observe(
            directory: contentDirectory,
            filter: "*",
            recursive: true,
            writer: this.Changes);

        using FileSystemWatcher settings = Observe(
            directory: Path.GetDirectoryName(configurationFile) ?? root,
            filter: Path.GetFileName(configurationFile),
            recursive: false,
            writer: this.Changes);

        await this
            .RebuildOnChangeAsync(contentDirectory, outputDirectory, basePath, configurationFile, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Reads the reported changes until the operation is canceled, rebuilding once per debounce window.
    /// </summary>
    /// <remarks>
    ///     A rebuild that fails is reported and the loop keeps running: a half-saved file that does not parse
    ///     is the normal case in an editor, and a preview server that exits on it is unusable. A reload that
    ///     fails is reported the same way, and the loop keeps the settings it last loaded, so a later change
    ///     to a page still rebuilds.
    /// </remarks>
    /// <param name="contentDirectory">
    ///     The absolute path of the directory the watched sources sit in, as the loop was started against it.
    /// </param>
    /// <param name="outputDirectory">
    ///     The absolute path of the directory the server answers from, as the loop was started against it.
    /// </param>
    /// <param name="basePath">
    ///     The path the server serves the site under, as the loop was started against it.
    /// </param>
    /// <param name="configurationFile">
    ///     The absolute path of the configuration file, whose change reloads the configuration before the
    ///     rebuild that follows it.
    /// </param>
    /// <param name="cancellationToken">
    ///     Token used to cancel the operation.
    /// </param>
    /// <returns>
    ///     A task that completes once the loop has stopped.
    /// </returns>
    internal async Task RebuildOnChangeAsync(
        string contentDirectory,
        string outputDirectory,
        string basePath,
        string configurationFile,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contentDirectory);
        ArgumentNullException.ThrowIfNull(outputDirectory);
        ArgumentNullException.ThrowIfNull(basePath);
        ArgumentNullException.ThrowIfNull(configurationFile);

        ChannelReader<string> reader = this._changes.Reader;

        while (!cancellationToken.IsCancellationRequested)
        {
            string trigger;

            try
            {
                trigger = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                await Task.Delay(DebounceWindow, this._timeProvider, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Ctrl+C is how the loop is meant to end, in the same way as the server beside it.
                break;
            }

            List<string> arrived = [trigger];

            while (reader.TryRead(out string? path))
                arrived.Add(path);

            List<string> changed = [];
            var considered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string path in arrived)
            {
                if (considered.Add(path) && this.HasChanged(path))
                    changed.Add(path);
            }

            if (changed.Count == 0) continue;

            trigger = changed[0];
            this._logger.LogChangeDetected(changed.Count, trigger);

            long started = Stopwatch.GetTimestamp();

            if (changed.Any(path => string.Equals(path, configurationFile, StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    this._options = this._reload();
                    this._logger.LogConfigurationReloaded(configurationFile);
                }
                catch (Exception exception) when (exception is ConfigurationException or IOException or UnauthorizedAccessException)
                {
                    this.ReportFailure(exception);
                    continue;
                }
            }

            SiteOptions options = this._options;

            if (this.Moved(contentDirectory, outputDirectory, basePath, options) is string moved)
            {
                this._stdErr.MarkupLineInterpolated($"[yellow]warning:[/] {moved} changed. Restart [bold]gelyn serve[/] to pick that up.");
                continue;
            }

            try
            {
                BuildReport report = await this._builder.BuildAsync(options, cancellationToken).ConfigureAwait(false);
                double elapsed = started.ElapsedMilliseconds;

                this._logger.LogRebuildCompleted(report.FileCount, elapsed);
                this.ReportRebuild(report.FileCount, elapsed, trigger, changed.Count);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception) when (IsBuildFailure(exception))
            {
                this.ReportFailure(exception);
            }
        }
    }

    #region Helpers

    /// <summary>
    ///     Reports whether the entry at one path has changed since the loop last saw it.
    /// </summary>
    /// <remarks>
    ///     A copy made from a file whose write time is recent raises a change on the source, not just the
    ///     destination. That turns the asset copying every rebuild performs into a loop - five copies of a
    ///     touched file raise five notifications, five of an untouched file none - each rebuild reporting the
    ///     change that begins the next one. Write time and length distinguish a real save from the echo, with
    ///     a missing path counting because nothing remains to compare, and a directory because it comes from
    ///     the <see cref="FileSystemWatcher.Error"/> handler and rebuilds everything.
    /// </remarks>
    /// <param name="path">
    ///     The absolute path a watcher reported.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> when the path is worth a rebuild.
    /// </returns>
    private bool HasChanged(string path)
    {
        if (this._fileSystem.Directory.Exists(path)) return true;

        IFileInfo entry = this._fileSystem.FileInfo.New(path);

        if (!entry.Exists)
        {
            this._entries.Remove(path);
            return true;
        }

        (DateTime Written, long Length) current = (entry.LastWriteTimeUtc, entry.Length);

        if (this._entries.TryGetValue(path, out (DateTime Written, long Length) previous) && previous == current)
            return false;

        this._entries[path] = current;

        return true;
    }

    /// <summary>
    ///     Starts one watcher that reports every change it sees to the loop.
    /// </summary>
    /// <remarks>
    ///     A handler writes the path and returns, leaving the callback thread free before the watcher's
    ///     buffer can fill. An overflow loses the events it could not hold, which the
    ///     <see cref="FileSystemWatcher.Error"/> handler answers with a rebuild of the whole site.
    /// </remarks>
    /// <param name="directory">
    ///     The directory to watch, which has to exist.
    /// </param>
    /// <param name="filter">
    ///     The pattern an entry's name has to match to be reported.
    /// </param>
    /// <param name="recursive">
    ///     Indicates whether the directories below <paramref name="directory"/> are watched as well.
    /// </param>
    /// <param name="writer">
    ///     Receives the absolute path of each reported entry.
    /// </param>
    /// <returns>
    ///     The started watcher, which stops raising events once it is disposed.
    /// </returns>
    private static FileSystemWatcher Observe(string directory, string filter, bool recursive, ChannelWriter<string> writer)
    {
        var watcher = new FileSystemWatcher(directory, filter) { IncludeSubdirectories = recursive };
        void Report(object sender, FileSystemEventArgs e) => writer.TryWrite(e.FullPath);

        watcher.Changed += Report;
        watcher.Created += Report;
        watcher.Deleted += Report;
        watcher.Renamed += Report;
        watcher.Error += (_, _) => writer.TryWrite(directory);
        watcher.EnableRaisingEvents = true;

        return watcher;
    }

    /// <summary>
    ///     The ways a build over a tree that is being edited fails, each of which leaves the loop serving
    ///     what it last built: a half-saved file whose front matter does not parse yet, a file that is locked
    ///     or gone, a landing page that is gone, and a tree that breaks a rule of the build halfway through an
    ///     edit.
    /// </summary>
    /// <param name="exception">
    ///     The exception the build failed with.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> when the next change is worth rebuilding on, <see langword="false"/> when
    ///     the exception is to fault the loop.
    /// </returns>
    private static bool IsBuildFailure(Exception exception) =>
        exception is BuildException or ConfigurationException or IOException or UnauthorizedAccessException or YamlException;

    /// <summary>
    ///     Writes what one failure has to say for itself.
    /// </summary>
    /// <param name="exception">
    ///     The exception the reload or the rebuild failed with.
    /// </param>
    private void ReportFailure(Exception exception) => this._stdErr.WriteError(exception);

    /// <summary>
    ///     Names the settings a reload has moved, which the watchers and the server cannot follow without being
    ///     rewired.
    /// </summary>
    /// <remarks>
    ///     Compared as resolved paths, never as the configured values, so that a cosmetic edit such as
    ///     <c>./content</c> for <c>content</c> is not reported as a move.
    /// </remarks>
    /// <param name="contentDirectory">
    ///     The absolute path of the watched content directory.
    /// </param>
    /// <param name="outputDirectory">
    ///     The absolute path of the served output directory.
    /// </param>
    /// <param name="basePath">
    ///     The path the server serves the site under.
    /// </param>
    /// <param name="options">
    ///     The site configuration as it reads now.
    /// </param>
    /// <returns>
    ///     The names of the settings that moved, or <see langword="null"/> when each still resolves to the
    ///     directory or the path the loop was started against.
    /// </returns>
    private string? Moved(string contentDirectory, string outputDirectory, string basePath, SiteOptions options)
    {
        string root = this._contentRoot;
        List<string> moved = [];

        if (!PathBoundary.Resolve(root, options.ContentDirectory).Equals(contentDirectory, StringComparison.Ordinal))
            moved.Add(nameof(SiteOptions.ContentDirectory));

        if (!PathBoundary.Resolve(root, options.OutputDirectory).Equals(outputDirectory, StringComparison.Ordinal))
            moved.Add(nameof(SiteOptions.OutputDirectory));

        if (!UrlUtils.ResolveBasePath(options.BaseUrl).Equals(basePath, StringComparison.Ordinal))
            moved.Add(nameof(SiteOptions.BaseUrl));

        return moved.Count > 0
            ? string.Join(" and ", moved)
            : null;
    }

    /// <summary>
    ///     Writes the one line a rebuild reports, a summary of the pass. The line per file that
    ///     <c>gelyn build</c> writes would turn every save into noise.
    /// </summary>
    /// <param name="fileCount">
    ///     The number of files the rebuild wrote, copied and deleted.
    /// </param>
    /// <param name="elapsedMilliseconds">
    ///     How long the rebuild took, the reload before it included.
    /// </param>
    /// <param name="trigger">
    ///     The absolute path of the change that opened the debounce window.
    /// </param>
    /// <param name="changeCount">
    ///     The number of entries that changed inside that window, <paramref name="trigger"/> included.
    /// </param>
    private void ReportRebuild(int fileCount, double elapsedMilliseconds, string trigger, int changeCount)
    {
        // Formatted with Tool.Culture, because interpolating the value would use the current one.
        string elapsed = elapsedMilliseconds.ToString("F2", Tool.Culture);

        string cause = changeCount == 1
            ? $"{Path.GetRelativePath(this._contentRoot, trigger)} changed"
            : string.Create(Tool.Culture, $"{changeCount} entries changed");

        this._stdOut.MarkupLineInterpolated($"[green]rebuilt[/] {fileCount} file(s) in {elapsed} ms after {cause}.");
    }

    #endregion
}
