using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO.Abstractions;
using System.Runtime.ExceptionServices;

using Gelyn.Core.Internals;
using Gelyn.Diagnostics;
using Gelyn.Extensions;
using Gelyn.Model;
using Gelyn.Model.Options;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

using Spectre.Console;

using Testably.Abstractions;

namespace Gelyn.Cli;

/// <summary>
///     Represents one run of the tool with its content root, its configuration file, its consoles and the services
///     the command resolves.
/// </summary>
/// <remarks>
///     A command action opens the session from its parse result, so <c>--help</c>, <c>--version</c>, a parse
///     error and tab completion load no file and build no services. <see cref="Open"/> loads the file and stores
///     a failure in <see cref="File"/> instead of throwing it, so that <c>--info</c> can report it and a command
///     receives it on its first read of <see cref="Services"/>.
/// </remarks>
internal sealed class Session : IDisposable
{
    // Prefixed, because names such as LANGUAGE, PORT and TITLE are set by desktops and hosts for their own use.
    private const string SettingVariablePrefix = "GELYN_";

    private readonly Func<ServiceProvider> _compose;
    private readonly IDisposable? _configuration;
    private ServiceProvider? _services;
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="Session"/> class.
    /// </summary>
    /// <param name="contentRoot">
    ///     The directory every relative setting is resolved against.
    /// </param>
    /// <param name="file">
    ///     The configuration file the run reads, and what loading it found.
    /// </param>
    /// <param name="fileSystem">
    ///     The file system the run works against.
    /// </param>
    /// <param name="stdOut">
    ///     The console the run writes its output to.
    /// </param>
    /// <param name="stdErr">
    ///     The console the run writes its warnings and failures to.
    /// </param>
    /// <param name="compose">
    ///     The factory that builds the services on the first read of <see cref="Services"/>.
    /// </param>
    /// <param name="configuration">
    ///     The configuration the services bind, disposed with the session, or <see langword="null"/> when the
    ///     factory owns its own.
    /// </param>
    internal Session(
        string contentRoot,
        ConfigurationFileStatus file,
        IFileSystem fileSystem,
        IAnsiConsole stdOut,
        IAnsiConsole stdErr,
        Func<ServiceProvider> compose,
        IDisposable? configuration = null)
    {
        this.ContentRoot = contentRoot;
        this.File = file;
        this.FileSystem = fileSystem;
        this.StdOut = stdOut;
        this.StdErr = stdErr;
        this._compose = compose;
        this._configuration = configuration;
    }

    /// <summary>
    ///     The directory every relative setting is resolved against.
    /// </summary>
    public string ContentRoot { get; }

    /// <summary>
    ///     The configuration file of the run and the outcome of loading it.
    /// </summary>
    public ConfigurationFileStatus File { get; }

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
    ///     The service provider of the run, built on the first read.
    /// </summary>
    /// <exception cref="FileNotFoundException">
    ///     The file given to <c>--config</c> does not exist.
    /// </exception>
    /// <exception cref="InvalidDataException">
    ///     The configuration file does not parse.
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
    public IServiceProvider Services
    {
        get
        {
            ObjectDisposedException.ThrowIf(this._disposed, this);

            // Raised here and not on opening, so that --info reports the failure a command stops with.
            if (this.File.Failure is Exception failure) ExceptionDispatchInfo.Throw(failure);

            return this._services ??= this._compose();
        }
    }

    /// <summary>
    ///     Creates the session of a parsed command line. The configuration file is loaded with the <c>GELYN_</c>
    ///     environment variables and the promoted options layered over it, and the service composition is
    ///     deferred to the first read of <see cref="Services"/>.
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

        var configuration = new ConfigurationManager();
        ConfigurationFileStatus file = LoadFile(configuration, ConfigurationFile.Resolve(contentRoot, requested), optional: requested is null);

        // The documented order, in which the variables outrank the file and the flags outrank both.
        configuration.AddEnvironmentVariables(prefix: SettingVariablePrefix);
        configuration.AddInMemoryCollection(ReadOverrides(parseResult));

        var fileSystem = new RealFileSystem();

        return new Session(contentRoot, file, fileSystem, stdOut, stdErr, Compose, configuration);

        ServiceProvider Compose()
        {
            var services = new ServiceCollection();

            services.AddLogging(logging => logging.ConfigureDiagnostics(level));
            services.AddSingleton<IConfiguration>(configuration);
            services.AddSingleton<IConfigurationRoot>(configuration);
            services.AddKeyedSingleton(ConsoleTarget.StdOut, stdOut);
            services.AddKeyedSingleton(ConsoleTarget.StdErr, stdErr);
            services.AddSingleton<IFileSystem>(fileSystem);
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton(file);
            services.AddSiteOptions(contentRoot, file);
            services.AddServices(contentRoot);

            ServiceProvider provider = services.BuildServiceProvider();

            provider.GetRequiredService<ILogger<Session>>().LogStartupCompleted(started.ElapsedMilliseconds);

            return provider;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #region Helpers

    // Private rather than protected virtual, since the class is sealed, and never called with false, since
    // nothing here needs a finalizer.
    private void Dispose(bool disposing)
    {
        if (this._disposed) return;

        if (disposing)
        {
            this._services?.Dispose();
            this._configuration?.Dispose();
        }

        this._disposed = true;
    }

    // Rooted, so that the source reads the file through a provider of its own directory. A provider of the
    // content root would refuse a path that climbs out of it, such as ../shared.json. Not watched, because the
    // watch costs most of the startup and serve runs its own watcher.
    private static ConfigurationFileStatus LoadFile(ConfigurationManager configuration, string path, bool optional)
    {
        JsonConfigurationSource? source = null;
        Exception? failure = null;

        try
        {
            configuration.AddJsonFile(json =>
            {
                json.Path = path;
                json.Optional = optional;
                json.ReloadOnChange = false;
                json.ResolveFileProvider();
                source = json;
            });
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            failure = exception;
        }

        return new ConfigurationFileStatus
        {
            Path = path,
            Optional = optional,
            Exists = source is { FileProvider: IFileProvider provider, Path: string name } && provider.GetFileInfo(name).Exists,
            Failure = failure,
        };
    }

    // Read from the parse, because only the parse knows every syntax the tool accepts, such as --option=value,
    // response files and options that take no value.
    private static Dictionary<string, string?> ReadOverrides(ParseResult parseResult)
    {
        (string Option, string Key)[] promotions =
        [
            (OverrideOptions.Title, nameof(SiteOptions.Title)),
            (OverrideOptions.BaseUrl, nameof(SiteOptions.BaseUrl)),
            (OverrideOptions.Theme, nameof(SiteOptions.Theme)),
            (OverrideOptions.Feed, nameof(SiteOptions.EnableFeed)),
            (OverrideOptions.Sitemap, nameof(SiteOptions.EnableSitemap)),
            (OverrideOptions.Content, nameof(SiteOptions.ContentDirectory)),
            (OverrideOptions.Output, nameof(SiteOptions.OutputDirectory)),
            (OverrideOptions.Port, nameof(SiteOptions.Port)),
        ];

        Dictionary<string, string?> overrides = [];

        foreach ((string option, string key) in promotions)
            if (ReadValue(parseResult, option) is string value)
                overrides[key] = value;

        if (ReadValue(parseResult, OverrideOptions.Exclude) is string patterns)
        {
            string[] split = patterns.Split(OverrideOptions.ExcludeSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            for (int index = 0; index < split.Length; index++)
            {
                string key = $"{nameof(SiteOptions.ExcludePatterns)}:{OverrideOptions.ExcludeOffset + index}";
                overrides[key] = split[index];
            }
        }

        return overrides;
    }

    // The token as given, so that the binder converts it as it converts the same value from the file.
    private static string? ReadValue(ParseResult parseResult, string option) =>
        parseResult.GetResult($"--{option}") is OptionResult { Implicit: false, Tokens: [.., Token token] } ? token.Value : null;

    #endregion
}
