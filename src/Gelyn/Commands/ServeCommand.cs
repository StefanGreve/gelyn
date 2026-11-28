using System.CommandLine;

using Gelyn.Cli;
using Gelyn.Core.Internals.Primitives;
using Gelyn.Model;
using Gelyn.Model.Options;
using Gelyn.Services;

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
    /// <param name="open">
    ///     The factory that opens the session of a parsed command line.
    /// </param>
    public ServeCommand(Func<ParseResult, Session> open)
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

        this.SetAction(async (parseResult, cancellationToken) =>
        {
            using Session session = open(parseResult);
            SiteBuilder siteBuilder = session.Builder;
            SiteOptions site = session.Options;

            try
            {
                BuildReport report = await siteBuilder
                    .BuildAsync(site, cancellationToken)
                    .ConfigureAwait(false);

                string outputDirectory = session.FileSystem.Path.Combine(session.ContentRoot, site.OutputDirectory);

                session.StdOut.WriteLine(BuildCommand.ComposeSummary(session.StdOut, report));

                using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                Task serving = session.Server.ServeAsync(
                    outputDirectory,
                    UrlUtils.ResolveBasePath(site.BaseUrl),
                    site.Port,
                    parseResult.GetValue(launchBrowser),
                    parseResult.GetValue(refresh),
                    lifetime.Token);

                Task watching = session.Watcher.WatchAsync(lifetime.Token);

                // A faulted rebuild loop has to end the serving too, so both are canceled as soon as either side finishes.
                await Task.WhenAny(serving, watching).ConfigureAwait(false);
                await lifetime.CancelAsync().ConfigureAwait(false);
                await Task.WhenAll(serving, watching).ConfigureAwait(false);

                return ExitCodes.Success;
            }
            catch (OperationCanceledException)
            {
                // Ctrl+C is how this command is meant to end, so it is not a failure.
                return ExitCodes.Success;
            }
        });
    }
}
