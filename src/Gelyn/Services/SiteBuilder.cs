using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
    private readonly ContentWalker _walker;
    private readonly IHostEnvironment _environment;
    private readonly IOptionsMonitor<SiteOptions> _options;
    private readonly IFileSystem _fileSystem;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SiteBuilder> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SiteBuilder"/> class.
    /// </summary>
    /// <param name="walker">
    ///     Discovers and renders the pages the site is made of.
    /// </param>
    /// <param name="environment">
    ///     Supplies the root that a relative output directory is resolved against.
    /// </param>
    /// <param name="options">
    ///     Supplies the site configuration the build is rendered against.
    /// </param>
    /// <param name="fileSystem">
    ///     Writes the generated pages.
    /// </param>
    /// <param name="timeProvider">
    ///     Supplies the date the generated pages are stamped with.
    /// </param>
    /// <param name="logger">
    ///     Receives the counters the build reports.
    /// </param>
    public SiteBuilder(
        ContentWalker walker,
        IHostEnvironment environment,
        IOptionsMonitor<SiteOptions> options,
        IFileSystem fileSystem,
        TimeProvider timeProvider,
        ILogger<SiteBuilder> logger)
    {
        this._walker = walker;
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

        IReadOnlyList<ContentPage> pages = await this._walker
            .WalkAsync(options, cancellationToken)
            .ConfigureAwait(false);

        long walkTicks = isMeasuring ? Stopwatch.GetTimestamp() - started : 0;

        IReadOnlyList<ContentPage> navigation = [.. pages.Where(static page => page.InNavigation)];
        DateOnly generatedAt = DateOnly.FromDateTime(this._timeProvider.GetUtcNow().UtcDateTime);

        IPath path = this._fileSystem.Path;
        string outputDirectory = path.Combine(this._environment.ContentRootPath, options.OutputDirectory);
        var written = new List<string>(pages.Count);
        long renderTicks = 0;
        long writeTicks = 0;

        foreach (ContentPage page in pages)
        {
            var context = new RenderContext
            {
                Options = options,
                Navigation = navigation,
                Page = page,
                GeneratedAt = generatedAt,
            };

            long renderStarted = isMeasuring ? Stopwatch.GetTimestamp() : 0;
            string html = PageLayout.Render(context);

            if (isMeasuring)
                renderTicks += Stopwatch.GetTimestamp() - renderStarted;

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
            double renderMs = Stopwatch.GetElapsedTime(0, renderTicks).TotalMilliseconds;
            double writeMs = Stopwatch.GetElapsedTime(0, writeTicks).TotalMilliseconds;
            double buildMs = elapsed.TotalMilliseconds;

            this._logger.LogWalkCompleted(pages.Count, walkMs);
            this._logger.LogRenderCompleted(pages.Count, renderMs);
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
