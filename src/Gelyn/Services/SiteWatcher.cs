using System.Diagnostics;
using System.IO.Abstractions;
using System.Threading.Channels;

using Gelyn.Core.Internals;
using Gelyn.Extensions;
using Gelyn.Model;
using Gelyn.Model.Options;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SharpYaml;

using Spectre.Console;

namespace Gelyn.Services;

/// <summary>
///     Rebuilds the site whenever a source file or the configuration file changes.
/// </summary>
/// <remarks>
///     Two watchers report into one channel the rebuild loop reads, so a rebuild never runs on a callback
///     thread, where a <see cref="FileSystemEventHandler"/> returning <see langword="void"/> would leave a
///     failure unobserved and block the watcher while its buffer fills. The tests drive that channel too,
///     the watcher factory of <c>TestableIO.System.IO.Abstractions.TestingHelpers</c> throwing from every
///     member.
/// </remarks>
internal sealed class SiteWatcher
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

    private readonly SiteBuilder _builder;
    private readonly IConfigurationRoot _configuration;
    private readonly IHostEnvironment _environment;
    private readonly IOptionsMonitor<SiteOptions> _options;
    private readonly IFileSystem _fileSystem;
    private readonly IAnsiConsole _console;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SiteWatcher> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SiteWatcher"/> class.
    /// </summary>
    /// <param name="builder">
    ///     Performs the generation each change triggers.
    /// </param>
    /// <param name="configuration">
    ///     The configuration the site options are bound from, read again when the file behind it changes.
    /// </param>
    /// <param name="environment">
    ///     The host environment relative paths are resolved against.
    /// </param>
    /// <param name="options">
    ///     Supplies the site configuration naming the directories to watch.
    /// </param>
    /// <param name="console">
    ///     The console the command writes its output to.
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
        SiteBuilder builder,
        IConfigurationRoot configuration,
        IHostEnvironment environment,
        IOptionsMonitor<SiteOptions> options,
        IAnsiConsole console,
        TimeProvider timeProvider,
        IFileSystem fileSystem,
        ILogger<SiteWatcher> logger)
    {
        this._builder = builder;
        this._configuration = configuration;
        this._environment = environment;
        this._options = options;
        this._console = console;
        this._timeProvider = timeProvider;
        this._fileSystem = fileSystem;
        this._logger = logger;
    }

    /// <summary>
    ///     The writer the watchers hand their events to, and the seam a test writes a change into.
    /// </summary>
    internal ChannelWriter<string> Changes => this._changes.Writer;

    /// <summary>
    ///     Watches the content tree and the configuration file until the operation is cancelled, rebuilding
    ///     the site once per burst of changes.
    /// </summary>
    /// <param name="cancellationToken">
    ///     Token used to cancel the operation.
    /// </param>
    /// <returns>
    ///     A task that completes once the loop has stopped.
    /// </returns>
    /// <exception cref="ArgumentException">
    ///     The content directory or the directory holding the configuration file does not exist.
    /// </exception>
    public async Task WatchAsync(CancellationToken cancellationToken)
    {
        SiteOptions options = this._options.CurrentValue;
        string root = this._environment.ContentRootPath;

        string contentDirectory = PathBoundary.Resolve(root, options.ContentDirectory);
        string outputDirectory = PathBoundary.Resolve(root, options.OutputDirectory);
        string configurationFile = PathBoundary.Resolve(root, this.ConfigurationFileName());

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
            .RebuildOnChangeAsync(contentDirectory, outputDirectory, configurationFile, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Reads the reported changes until the operation is cancelled, rebuilding once per debounce window.
    /// </summary>
    /// <remarks>
    ///     A rebuild that fails is reported and the loop keeps running: a half-saved file that does not parse
    ///     is the normal case in an editor, and a preview server that exits on it is unusable.
    /// </remarks>
    /// <param name="contentDirectory">
    ///     The absolute path of the directory the watched sources sit in, as the loop was started against it.
    /// </param>
    /// <param name="outputDirectory">
    ///     The absolute path of the directory the server answers from, as the loop was started against it.
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
        string configurationFile,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contentDirectory);
        ArgumentNullException.ThrowIfNull(outputDirectory);
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
            HashSet<string> considered = new(StringComparer.OrdinalIgnoreCase);

            foreach (string path in arrived)
            {
                if (considered.Add(path) && this.HasChanged(path))
                    changed.Add(path);
            }

            if (changed.Count == 0)
                continue;

            trigger = changed[0];
            this._logger.LogChangeDetected(changed.Count, trigger);

            long started = Stopwatch.GetTimestamp();
            SiteOptions options;

            try
            {
                if (changed.Any(path => string.Equals(path, configurationFile, StringComparison.OrdinalIgnoreCase)))
                {
                    this._configuration.Reload();
                    this._logger.LogConfigurationReloaded(configurationFile);
                }

                options = this._options.CurrentValue;
            }
            catch (Exception exception) when (IsConfigurationFailure(exception))
            {
                this.ReportFailure(exception);
                continue;
            }

            if (this.Moved(contentDirectory, outputDirectory, options) is string moved)
            {
                this._console.MarkupLineInterpolated($"[yellow]warning:[/] {moved} changed. Restart [bold]gelyn serve[/] to pick that up.");
                continue;
            }

            try
            {
                BuildReport report = await this._builder.BuildAsync(cancellationToken).ConfigureAwait(false);
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
        if (this._fileSystem.Directory.Exists(path))
            return true;

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
        FileSystemWatcher watcher = new(directory, filter) { IncludeSubdirectories = recursive };
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
    ///     The ways a configuration file that is being edited fails, each of which leaves the loop serving
    ///     what it last built: a file that does not parse, a value the binder cannot convert to the type of
    ///     the setting it belongs to, and values the validator rejects once it can.
    /// </summary>
    /// <remarks>
    ///     An aggregate counts when every failure in it does, because that is the shape these arrive in:
    ///     <see cref="IConfigurationRoot.Reload"/> raises the reload token through a
    ///     <see cref="CancellationTokenSource"/>, whose <c>Cancel</c> "aggregates any exceptions thrown into
    ///     an <see cref="AggregateException"/>", and the options monitor binds and validates eagerly in one
    ///     of those callbacks. The rejection comes out of the reload, nested once per token in the chain.
    /// </remarks>
    /// <param name="exception">
    ///     The exception the reload or the read of the options failed with.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> when the next change is worth rebuilding on, <see langword="false"/> when
    ///     the exception is to fault the loop.
    /// </returns>
    /// <seealso href="https://learn.microsoft.com/dotnet/api/system.threading.cancellationtokensource.cancel">
    ///     CancellationTokenSource.Cancel.
    /// </seealso>
    private static bool IsConfigurationFailure(Exception exception) => exception switch
    {
        AggregateException aggregate => aggregate.Flatten().InnerExceptions is { Count: > 0 } failures && failures.All(IsConfigurationFailure),
        InvalidDataException or InvalidOperationException or OptionsValidationException => true,
        IOException => true,
        _ => false,
    };

    /// <summary>
    ///     The ways a build over a tree that is being edited fails, each of which leaves the loop serving
    ///     what it last built: a half-saved file whose front matter does not parse yet, and a file that is
    ///     locked or gone.
    /// </summary>
    /// <param name="exception">
    ///     The exception the build failed with.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> when the next change is worth rebuilding on, <see langword="false"/> when
    ///     the exception is to fault the loop.
    /// </returns>
    private static bool IsBuildFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or YamlException;

    /// <summary>
    ///     Writes what one failure has to say for itself.
    /// </summary>
    /// <remarks>
    ///     An <see cref="AggregateException"/> is reported by its contents, because its own message nests one
    ///     "One or more errors occurred" per layer and names none of the failures underneath them.
    /// </remarks>
    /// <param name="exception">
    ///     The exception the reload or the rebuild failed with.
    /// </param>
    private void ReportFailure(Exception exception)
    {
        IEnumerable<string> messages = exception is AggregateException aggregate
            ? aggregate.Flatten().InnerExceptions.Select(static failure => failure.Message)
            : [exception.Message];

        foreach (string message in messages)
            this._console.MarkupLineInterpolated($"[red]error:[/] {message}");
    }

    private string ConfigurationFileName() =>
        this._configuration[ConfigurationFile.PathKey] ?? ConfigurationFile.DefaultFileName;

    /// <summary>
    ///     Names the directory settings a reload has moved, which the watchers and the server cannot follow
    ///     without being rewired.
    /// </summary>
    /// <remarks>
    ///     Compared as resolved paths rather than as configured values, so that a cosmetic edit such as
    ///     <c>./content</c> for <c>content</c> is not reported as a move.
    /// </remarks>
    /// <param name="contentDirectory">
    ///     The absolute path of the watched content directory.
    /// </param>
    /// <param name="outputDirectory">
    ///     The absolute path of the served output directory.
    /// </param>
    /// <param name="options">
    ///     The site configuration as it reads now.
    /// </param>
    /// <returns>
    ///     The names of the settings that moved, or <see langword="null"/> when both still resolve to the
    ///     directories the loop was started against.
    /// </returns>
    private string? Moved(string contentDirectory, string outputDirectory, SiteOptions options)
    {
        string root = this._environment.ContentRootPath;
        List<string> moved = [];

        if (!PathBoundary.Resolve(root, options.ContentDirectory).Equals(contentDirectory, StringComparison.Ordinal))
            moved.Add(nameof(SiteOptions.ContentDirectory));

        if (!PathBoundary.Resolve(root, options.OutputDirectory).Equals(outputDirectory, StringComparison.Ordinal))
            moved.Add(nameof(SiteOptions.OutputDirectory));

        return moved.Count > 0
            ? string.Join(" and ", moved)
            : null;
    }

    /// <summary>
    ///     Writes the one line a rebuild reports, which is a summary rather than the line per file that
    ///     <c>gelyn build</c> writes and that every save would turn into noise.
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
        // Formatted here rather than in the interpolation, so the culture is not left to the console.
        string elapsed = elapsedMilliseconds.ToString("F2", Tool.Culture);

        string cause = changeCount == 1
            ? $"{Path.GetRelativePath(this._environment.ContentRootPath, trigger)} changed"
            : string.Create(Tool.Culture, $"{changeCount} entries changed");

        this._console.MarkupLineInterpolated($"[green]rebuilt[/] {fileCount} file(s) in {elapsed} ms after {cause}.");
    }

    #endregion
}
