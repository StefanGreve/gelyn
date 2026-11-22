using System.CommandLine;
using System.IO.Abstractions;

using Gelyn.Abstractions;
using Gelyn.Core.Internals;
using Gelyn.Diagnostics;
using Gelyn.Extensions;
using Gelyn.Model.Options;
using Gelyn.Services;

using Microsoft.Extensions.Logging;

using Spectre.Console;

using Testably.Abstractions;

namespace Gelyn.Cli;

/// <summary>
///     Represents one run of the tool with its content root, its settings, its consoles and the services the
///     command resolves.
/// </summary>
internal sealed class Session : IDisposable
{
    private readonly LogLevel _verbosity;
    private readonly long _started;
    private DiagnosticLog? _log;
    private SiteOptions? _options;
    private SiteBuilderContract? _builder;
    private FileServerContract? _server;
    private SiteWatcherContract? _watcher;
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="Session"/> class.
    /// </summary>
    /// <param name="contentRoot">
    ///     The directory every relative setting is resolved against.
    /// </param>
    /// <param name="settings">
    ///     The settings of the run, loaded on the first read of <see cref="Options"/>.
    /// </param>
    /// <param name="fileSystem">
    ///     The file system the run reads and writes through.
    /// </param>
    /// <param name="stdOut">
    ///     The console the run writes its output to.
    /// </param>
    /// <param name="stdErr">
    ///     The console the run writes its warnings and failures to.
    /// </param>
    /// <param name="verbosity">
    ///     The minimum level the services log at.
    /// </param>
    /// <param name="started">
    ///     The timestamp the process started at, which the startup record is measured from.
    /// </param>
    internal Session(
        string contentRoot,
        SiteSettings settings,
        IFileSystem fileSystem,
        IAnsiConsole stdOut,
        IAnsiConsole stdErr,
        LogLevel verbosity,
        long started)
    {
        this.ContentRoot = contentRoot;
        this.Settings = settings;
        this.FileSystem = fileSystem;
        this.StdOut = stdOut;
        this.StdErr = stdErr;
        this._verbosity = verbosity;
        this._started = started;
    }

    /// <summary>
    ///     The directory every relative setting is resolved against.
    /// </summary>
    public string ContentRoot { get; }

    /// <summary>
    ///     The settings of the run and the file they are read from.
    /// </summary>
    public SiteSettings Settings { get; }

    /// <summary>
    ///     The file system the run reads and writes through.
    /// </summary>
    public IFileSystem FileSystem { get; }

    /// <summary>
    ///     The console the run writes its output to.
    /// </summary>
    public IAnsiConsole StdOut { get; }

    /// <summary>
    ///     The console the run writes its warnings and failures to.
    /// </summary>
    public IAnsiConsole StdErr { get; }

    /// <summary>
    ///     The settings of the run, loaded and validated on the first read.
    /// </summary>
    /// <exception cref="ConfigurationException">
    ///     A required file is missing, the file is not JSON, a value cannot be converted to the type of its
    ///     setting, or a setting fails a validation rule.
    /// </exception>
    /// <exception cref="IOException">
    ///     The configuration file cannot be opened.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    ///     The configuration file cannot be read.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    ///     The session has been disposed.
    /// </exception>
    public SiteOptions Options
    {
        get
        {
            ObjectDisposedException.ThrowIf(this._disposed, this);
            return this._options ??= this.Settings.Load();
        }
    }

    /// <summary>
    ///     The log the services write their records to, created on the first read.
    /// </summary>
    /// <exception cref="ObjectDisposedException">
    ///     The session has been disposed.
    /// </exception>
    public DiagnosticLog Log
    {
        get
        {
            ObjectDisposedException.ThrowIf(this._disposed, this);
            return this._log ??= this.CreateLog();
        }
    }

    /// <summary>
    ///     The builder that writes the site, constructed on the first read.
    /// </summary>
    public SiteBuilderContract Builder =>
        this._builder ??= new SiteBuilder(
            new ContentPipeline(this.ContentRoot, this.FileSystem),
            this.ContentRoot,
            this.FileSystem,
            this.Log.CreateLogger<SiteBuilder>()
        );

    /// <summary>
    ///     The server that answers requests for the written site, constructed on the first read.
    /// </summary>
    public FileServerContract Server =>
        this._server ??= new FileServer(new StaticFileResolver(this.FileSystem), this.FileSystem, this.StdOut, this.StdErr, TimeProvider.System, this.Log.CreateLogger<FileServer>());

    /// <summary>
    ///     The watcher that rebuilds the site on a change, constructed on the first read.
    /// </summary>
    /// <exception cref="ConfigurationException">
    ///     The settings the watcher starts from are rejected, as <see cref="Options"/> raises it.
    /// </exception>
    public SiteWatcherContract Watcher =>
        this._watcher ??= new SiteWatcher(
            this.Builder,
            this.Options,
            this.Settings.Load,
            this.ContentRoot,
            this.Settings.Path,
            this.StdOut,
            this.StdErr,
            TimeProvider.System,
            this.FileSystem,
            this.Log.CreateLogger<SiteWatcher>()
        );

    /// <summary>
    ///     Creates the session of a parsed command line, with the configuration file resolved from
    ///     <c>--config</c> and the log level from <c>--verbosity</c>.
    /// </summary>
    /// <param name="parseResult">
    ///     The result of parsing the full command tree, without errors.
    /// </param>
    /// <param name="contentRoot">
    ///     The directory every relative setting is resolved against.
    /// </param>
    /// <param name="stdOut">
    ///     The console the run writes its output to.
    /// </param>
    /// <param name="stdErr">
    ///     The console the run writes its warnings and failures to.
    /// </param>
    /// <param name="started">
    ///     The timestamp the process started at, which the startup record is measured from.
    /// </param>
    /// <returns>
    ///     The session, which the caller disposes when the command completes.
    /// </returns>
    public static Session Open(ParseResult parseResult, string contentRoot, IAnsiConsole stdOut, IAnsiConsole stdErr, long started)
    {
        ArgumentNullException.ThrowIfNull(parseResult);

        string? requested = parseResult.GetValue<string>(ConfigurationFile.OptionName);
        LogLevel level = parseResult.GetValue<LogLevel>(VerbosityOption.OptionName);

        var fileSystem = new RealFileSystem();
        var settings = new SiteSettings(
            contentRoot,
            ConfigurationFile.Resolve(contentRoot, requested),
            optional: requested is null,
            parseResult, fileSystem
        );

        return new Session(
            contentRoot,
            settings,
            fileSystem,
            stdOut,
            stdErr,
            level,
            started
        );
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #region Helpers

    private void Dispose(bool disposing)
    {
        if (this._disposed) return;

        if (disposing) this._log?.Dispose();

        this._disposed = true;
    }

    // The startup record is written here, where the first service asks for a logger, so a run that reads no
    // service writes nothing.
    private DiagnosticLog CreateLog()
    {
        var log = new DiagnosticLog(
            this._verbosity,
            this.StdErr.Profile.Out.Writer,
            SupportsColor(this.StdErr),
            TimeProvider.System
        );

        log.CreateLogger<Session>().LogStartupCompleted(this._started.ElapsedMilliseconds);
        return log;
    }

    // Spectre has already detected the terminal, NO_COLOR and a legacy console for the error stream.
    private static bool SupportsColor(IAnsiConsole console) =>
        console.Profile.Capabilities.Ansi && console.Profile.Capabilities.ColorSystem != ColorSystem.NoColors;

    #endregion
}
