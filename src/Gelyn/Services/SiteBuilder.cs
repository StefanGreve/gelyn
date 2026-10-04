using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Gelyn.Components;
using Gelyn.Core;
using Gelyn.Internals;
using Gelyn.Model;
using Gelyn.Model.Options;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gelyn.Services;

/// <summary>
///     Renders every discovered page and writes the results to the output directory.
/// </summary>
public sealed class SiteBuilder
{
    private readonly ContentPipeline _pipeline;
    private readonly IHostEnvironment _environment;
    private readonly IOptionsMonitor<SiteOptions> _options;
    private readonly IFileSystem _fileSystem;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SiteBuilder> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SiteBuilder"/> class.
    /// </summary>
    /// <param name="pipeline">
    ///     Discovers and renders the pages the site is made of.
    /// </param>
    /// <param name="environment">
    ///     The host environment relative paths are resolved against.
    /// </param>
    /// <param name="options">
    ///     Supplies the site configuration the build is rendered against.
    /// </param>
    /// <param name="fileSystem">
    ///     The file system to work against.
    /// </param>
    /// <param name="timeProvider">
    ///     Supplies the current date and time.
    /// </param>
    /// <param name="logger">
    ///     Receives the records the build writes.
    /// </param>
    public SiteBuilder(
        ContentPipeline pipeline,
        IHostEnvironment environment,
        IOptionsMonitor<SiteOptions> options,
        IFileSystem fileSystem,
        TimeProvider timeProvider,
        ILogger<SiteBuilder> logger)
    {
        this._pipeline = pipeline;
        this._environment = environment;
        this._options = options;
        this._fileSystem = fileSystem;
        this._timeProvider = timeProvider;
        this._logger = logger;
    }

    /// <summary>
    ///     Generates the whole site.
    /// </summary>
    /// <param name="cancellationToken">
    ///     Token used to cancel the operation.
    /// </param>
    /// <returns>
    ///     The files that were written and how long writing them took.
    /// </returns>
    public async Task<BuildReport> BuildAsync(CancellationToken cancellationToken)
    {
        SiteOptions options = this._options.CurrentValue;
        long started = Stopwatch.GetTimestamp();

        // The total is reported either way, so only the per-page detail is worth guarding.
        bool isMeasuring = this._logger.IsEnabled(LogLevel.Debug);

        IReadOnlyList<ContentPage> pages = await this._pipeline
            .WalkAsync(options, cancellationToken)
            .ConfigureAwait(false);

        long walkTicks = isMeasuring ? Stopwatch.GetTimestamp() - started : 0;

        IReadOnlyList<ContentPage> navigation = [.. pages.Where(static page => page.InNavigation)];
        DateOnly generatedAt = DateOnly.FromDateTime(this._timeProvider.GetUtcNow().UtcDateTime);

        IPath path = this._fileSystem.Path;
        string outputDirectory = path.Combine(this._environment.ContentRootPath, options.OutputDirectory);
        var written = new List<string>(pages.Count);
        long composeTicks = 0;
        long writeTicks = 0;

        // The footer is invariant, so one component serves every page. The walk guarantees pages is not empty.
        string footer = FooterComponent.Render(pages[0].ToRenderContext(options, navigation, generatedAt));

        // The banner differs only in which navigation entry carries aria-current, so it needs one render per
        // linked page plus one shared by every page the navigation leaves out, however many pages there are.
        Dictionary<string, string> banners = new(navigation.Count + 1, StringComparer.Ordinal);

        foreach (ContentPage page in pages)
        {
            RenderContext context = page.ToRenderContext(options, navigation, generatedAt);

            long composeStarted = isMeasuring ? Stopwatch.GetTimestamp() : 0;
            string key = page.InNavigation ? page.Href : string.Empty;

            if (!banners.TryGetValue(key, out string? header))
            {
                header = HeaderComponent.Render(context);
                banners[key] = header;
            }

            string html = PageLayout.Render(context, header, footer);

            if (isMeasuring)
                composeTicks += Stopwatch.GetTimestamp() - composeStarted;

            string destination = path.Combine(outputDirectory, page.OutputPath);
            long writeStarted = isMeasuring ? Stopwatch.GetTimestamp() : 0;

            this._fileSystem.Directory.CreateDirectory(path.GetDirectoryName(destination) ?? outputDirectory);

            await this._fileSystem.File
                .WriteAllTextAsync(destination, html, cancellationToken)
                .ConfigureAwait(false);

            if (isMeasuring)
                writeTicks += Stopwatch.GetTimestamp() - writeStarted;

            written.Add(page.OutputPath);
        }

        // Taken before the reports below, whose console writes would otherwise be counted in the build total.
        TimeSpan elapsed = Stopwatch.GetElapsedTime(started);

        if (isMeasuring)
        {
            double walkMs = Stopwatch.GetElapsedTime(0, walkTicks).TotalMilliseconds;
            double composeMs = Stopwatch.GetElapsedTime(0, composeTicks).TotalMilliseconds;
            double writeMs = Stopwatch.GetElapsedTime(0, writeTicks).TotalMilliseconds;
            double buildMs = elapsed.TotalMilliseconds;

            this._logger.LogWalkCompleted(pages.Count, walkMs);
            this._logger.LogComposeCompleted(pages.Count, composeMs);
            this._logger.LogWriteCompleted(written.Count, writeMs);
            this._logger.LogBuildCompleted(written.Count, buildMs);
        }

        return new BuildReport
        {
            Files = written,
            Elapsed = elapsed,
        };
    }
}
