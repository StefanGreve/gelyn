using System.CommandLine;
using System.IO.Abstractions;
using System.Net;

using Gelyn.Abstractions;
using Gelyn.Core.Internals;
using Gelyn.Model;
using Gelyn.Model.Options;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Spectre.Console;

namespace Gelyn.Commands;

/// <summary>
///     Generates the site, serves it over HTTP for local preview, and generates it again whenever a source
///     file or the configuration file changes.
/// </summary>
internal sealed class ServeCommand : Command
{
    private const string LaunchBrowserOption = "launch-browser";
    private const string RefreshOption = "refresh";

    /// <summary>
    ///     Initializes a new instance of the <see cref="ServeCommand"/> class.
    /// </summary>
    /// <param name="siteBuilder">
    ///     Performs the generation that precedes the serving.
    /// </param>
    /// <param name="server">
    ///     Answers the requests for what the generation produced.
    /// </param>
    /// <param name="watcher">
    ///     Generates the site again whenever a source file or the configuration file changes.
    /// </param>
    /// <param name="environment">
    ///     The host environment relative paths are resolved against.
    /// </param>
    /// <param name="options">
    ///     Supplies the site configuration naming the directory to serve.
    /// </param>
    /// <param name="fileSystem">
    ///     The file system to work against.
    /// </param>
    /// <param name="stdOut">
    ///     The console the command writes its output to.
    /// </param>
    /// <param name="stdErr">
    ///     The console the command writes its failures to.
    /// </param>
    public ServeCommand(
        SiteBuilderContract siteBuilder,
        FileServerContract server,
        SiteWatcherContract watcher,
        IHostEnvironment environment,
        IOptionsMonitor<SiteOptions> options,
        IFileSystem fileSystem,
        [FromKeyedServices(ConsoleTarget.StdOut)] IAnsiConsole stdOut,
        [FromKeyedServices(ConsoleTarget.StdErr)] IAnsiConsole stdErr)
        : base("serve", "Serve the site over HTTP for local preview, rebuilding it as its sources change.")
    {
        // Carries its value into SiteOptions.Port like the shared overrides, so the range is checked there.
        var port = new Option<int>($"--{OverrideOptions.Port}")
        {
            Description = "The loopback port to serve on.",
            HelpName = "number",
        };

        var launchBrowser = new Option<bool>($"--{LaunchBrowserOption}")
        {
            Description = "Open the served site in the default browser.",
        };

        var refresh = new Option<int?>($"--{RefreshOption}")
        {
            Description = "Ask the browser to reload an HTML page every so many seconds. Absent by default.",
            HelpName = "seconds",
        };

        // Zero would leave no gap between passes, so the browser would reload without ever settling.
        refresh.Validators.Add(static result =>
        {
            if (result.GetValueOrDefault<int?>() is < 1)
                result.AddError($"--{RefreshOption} must be at least 1.");
        });

        this.Options.Add(port);
        this.Options.Add(launchBrowser);
        this.Options.Add(refresh);

        // Serving implies building, so the build's own options apply here unchanged.
        foreach (Option option in OverrideOptions.Definitions)
            this.Options.Add(option);

        this.SetAction(async (parsed, cancellationToken) =>
        {
            try
            {
                BuildReport report = await siteBuilder
                    .BuildAsync(cancellationToken)
                    .ConfigureAwait(false);

                SiteOptions site = options.CurrentValue;
                string outputDirectory = fileSystem.Path.Combine(environment.ContentRootPath, site.OutputDirectory);

                string elapsed = report.Elapsed.TotalMilliseconds.ToString("F2", Tool.Culture);
                stdOut.MarkupLineInterpolated($"Processed [bold]{report.FileCount}[/] file(s) in [bold]{elapsed}[/] ms.");

                using var session = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                Task serving = server.ServeAsync(
                    outputDirectory,
                    site.Port,
                    parsed.GetValue(launchBrowser),
                    parsed.GetValue(refresh),
                    session.Token);

                Task watching = watcher.WatchAsync(session.Token);

                // A faulted rebuild loop has to end the serving too, so the session is cancelled as soon as either side finishes.
                await Task.WhenAny(serving, watching).ConfigureAwait(false);
                await session.CancelAsync().ConfigureAwait(false);
                await Task.WhenAll(serving, watching).ConfigureAwait(false);

                return ExitCodes.Success;
            }
            catch (OperationCanceledException)
            {
                // Ctrl+C is how this command is meant to end, so it is not a failure.
                return ExitCodes.Success;
            }
            catch (Exception exception) when (exception is not FileNotFoundException and (IOException or HttpListenerException or PlatformNotSupportedException))
            {
                stdErr.MarkupLineInterpolated($"[red]error:[/] {exception.Message}");
                return ExitCodes.Error;
            }
        });
    }
}
