using System;
using System.CommandLine;
using System.IO;
using System.IO.Abstractions;
using System.Net;

using Gelyn.Core.Internals;
using Gelyn.Model;
using Gelyn.Model.Options;
using Gelyn.Services;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Spectre.Console;

namespace Gelyn.Commands;

/// <summary>
///     Generates the site and serves it over HTTP for local preview.
/// </summary>
internal sealed class ServeCommand : Command
{
    private const string PortOption = "port";
    private const string LaunchBrowserOption = "launch-browser";

    /// <summary>
    ///     The port served on when <c>--port</c> is absent.
    /// </summary>
    /// <remarks>
    ///     A default has to bind without privileges, which rules out everything below 1024, and stay clear of
    ///     49152-65535, where the kernel draws the ephemeral ports it hands to outbound connections and could
    ///     take this one first.
    /// </remarks>
    private const int DefaultPort = 9_624;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ServeCommand"/> class.
    /// </summary>
    /// <param name="siteBuilder">
    ///     Performs the generation that precedes the serving.
    /// </param>
    /// <param name="server">
    ///     Answers the requests for what the generation produced.
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
    /// <param name="console">
    ///     The console the command writes its output to.
    /// </param>
    public ServeCommand(
        SiteBuilder siteBuilder,
        FileServer server,
        IHostEnvironment environment,
        IOptionsMonitor<SiteOptions> options,
        IFileSystem fileSystem,
        IAnsiConsole console)
        : base("serve", "Generate the site and serve it over HTTP for local preview.")
    {
        Option<int> port = new($"--{PortOption}")
        {
            Description = $"The loopback port to serve on. Defaults to {DefaultPort}.",
            HelpName = "number",
            DefaultValueFactory = static _ => DefaultPort,
        };

        // Rejected here rather than left to bind, because HttpListener cannot take port 0 and reports the
        // refusal as a platform error code.
        port.Validators.Add(static result =>
        {
            if (result.GetValueOrDefault<int>() is <= IPEndPoint.MinPort or > IPEndPoint.MaxPort)
                result.AddError($"--{PortOption} must be between {IPEndPoint.MinPort + 1} and {IPEndPoint.MaxPort}.");
        });

        Option<bool> launchBrowser = new($"--{LaunchBrowserOption}")
        {
            Description = "Open the served site in the default browser.",
        };

        this.Options.Add(port);
        this.Options.Add(launchBrowser);

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
                int chosen = parsed.GetValue(port);

                string elapsed = report.Elapsed.TotalMilliseconds.ToString("F2", Tool.Culture);
                int processed = report.Pages.Count + report.Copies.Count + report.Removed.Count;

                console.MarkupLineInterpolated($"Processed [bold]{processed}[/] file(s) in [bold]{elapsed}[/] ms.");

                await server
                    .ServeAsync(outputDirectory, chosen, parsed.GetValue(launchBrowser), cancellationToken)
                    .ConfigureAwait(false);

                return ExitCodes.Success;
            }
            catch (OperationCanceledException)
            {
                // Ctrl+C is how this command is meant to end, so it is not a failure.
                return ExitCodes.Success;
            }
            catch (Exception exception) when (exception is IOException or HttpListenerException or PlatformNotSupportedException)
            {
                console.MarkupLineInterpolated($"[red]error:[/] {exception.Message}");

                return ExitCodes.Error;
            }
        });
    }
}
