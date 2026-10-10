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
///     Writes the finished site to the output directory.
/// </summary>
/// <remarks>
///     The build is processed in this order:
///     <list type="number">
///         <item>
///             <description><see cref="WritePagesAsync"/> composes and writes the rendered pages.</description>
///         </item>
///         <item>
///             <description><see cref="CopyAssets"/> copies every located file that is not a page.</description>
///         </item>
///     </list>
/// </remarks>
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
    ///     The files that were written, the files that were copied, and how long producing them took.
    /// </returns>
    public async Task<BuildReport> BuildAsync(CancellationToken cancellationToken)
    {
        SiteOptions options = this._options.CurrentValue;
        long started = Stopwatch.GetTimestamp();

        // The total is reported either way, so only the per-page detail is worth guarding.
        bool isMeasuring = this._logger.IsEnabled(LogLevel.Debug);

        IReadOnlyList<ContentItem> items = this._pipeline.Scan(options);
        long scanTicks = isMeasuring ? Stopwatch.GetTimestamp() - started : 0;

        IReadOnlyList<ContentItem> sources = [.. items.Where(static item => item.Kind is ContentKind.Page)];

        IReadOnlyList<ContentPage> pages = await this._pipeline
            .RenderAsync(sources, cancellationToken)
            .ConfigureAwait(false);

        long renderTicks = isMeasuring ? Stopwatch.GetTimestamp() - started - scanTicks : 0;

        string outputDirectory = this._fileSystem.Path.Combine(
            this._environment.ContentRootPath,
            options.OutputDirectory);

        (IReadOnlyList<string> written, long composeTicks, long writeTicks) = await this
            .WritePagesAsync(pages, options, outputDirectory, isMeasuring, cancellationToken)
            .ConfigureAwait(false);

        long copyStarted = isMeasuring ? Stopwatch.GetTimestamp() : 0;
        List<string> copied = this.CopyAssets(items, outputDirectory, cancellationToken);

        if (isMeasuring)
            writeTicks += Stopwatch.GetTimestamp() - copyStarted;

        // Taken before the reports below, whose console writes would otherwise be counted in the build total.
        TimeSpan elapsed = Stopwatch.GetElapsedTime(started);

        if (isMeasuring)
        {
            this.ReportCounters(
                items.Count,
                pages.Count,
                written.Count + copied.Count,
                (scanTicks, renderTicks, composeTicks, writeTicks),
                elapsed);
        }

        return new BuildReport
        {
            Pages = written,
            Copies = copied,
            Elapsed = elapsed,
        };
    }

    #region Helpers

    private async Task<(List<string> Written, long ComposeTicks, long WriteTicks)> WritePagesAsync(
        IReadOnlyList<ContentPage> pages,
        SiteOptions options,
        string outputDirectory,
        bool isMeasuring,
        CancellationToken cancellationToken)
    {
        IPath path = this._fileSystem.Path;
        IReadOnlyList<ContentPage> navigation = [.. pages.Where(static page => page.InNavigation)];
        DateOnly generatedAt = DateOnly.FromDateTime(this._timeProvider.GetUtcNow().UtcDateTime);

        var written = new List<string>(pages.Count);
        long composeTicks = 0;
        long writeTicks = 0;

        // The footer is invariant, so one component serves every page. The scan guarantees pages is not empty.
        string footer = FooterComponent.Render(pages[0].ToRenderContext(options, navigation, generatedAt));

        // The banner differs only in which navigation entry carries aria-current, so it needs one render per
        // linked page plus one shared by every page the navigation leaves out, however many pages there are.
        Dictionary<string, string> banners = new(navigation.Count + 1, StringComparer.Ordinal);

        foreach (ContentPage page in pages)
        {
            RenderContext context = page.ToRenderContext(options, navigation, generatedAt);

            long composeStarted = isMeasuring ? Stopwatch.GetTimestamp() : 0;
            string html = ComposeDocument(context, footer, banners);

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

        return (written, composeTicks, writeTicks);
    }

    private static string ComposeDocument(
        RenderContext context,
        string footer,
        Dictionary<string, string> banners)
    {
        string key = context.Page.InNavigation ? context.Page.Href : string.Empty;

        if (!banners.TryGetValue(key, out string? header))
        {
            header = HeaderComponent.Render(context);
            banners[key] = header;
        }

        return PageLayout.Render(context, header, footer);
    }

    private List<string> CopyAssets(
        IReadOnlyList<ContentItem> items,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        IPath path = this._fileSystem.Path;
        List<string> copied = [];

        foreach (ContentItem item in items.Where(static item => item.Kind is not ContentKind.Page))
        {
            cancellationToken.ThrowIfCancellationRequested();

            string destination = path.Combine(outputDirectory, item.OutputPath);
            this._fileSystem.Directory.CreateDirectory(path.GetDirectoryName(destination) ?? outputDirectory);
            this._fileSystem.File.Copy(item.SourcePath, destination, overwrite: true);

            copied.Add(item.OutputPath);
        }

        return copied;
    }

    private void ReportCounters(
        int itemCount,
        int pageCount,
        int fileCount,
        (long Scan, long Render, long Compose, long Write) ticks,
        TimeSpan elapsed)
    {
        double scanMs = ElapsedMilliseconds(ticks.Scan);
        double renderMs = ElapsedMilliseconds(ticks.Render);
        double composeMs = ElapsedMilliseconds(ticks.Compose);
        double writeMs = ElapsedMilliseconds(ticks.Write);
        double buildMs = elapsed.TotalMilliseconds;

        this._logger.LogScanCompleted(itemCount, scanMs);
        this._logger.LogRenderCompleted(pageCount, renderMs);
        this._logger.LogComposeCompleted(pageCount, composeMs);
        this._logger.LogWriteCompleted(fileCount, writeMs);
        this._logger.LogBuildCompleted(fileCount, buildMs);
    }

    private static double ElapsedMilliseconds(long ticks) =>
        Stopwatch.GetElapsedTime(0, ticks).TotalMilliseconds;

    #endregion
}
