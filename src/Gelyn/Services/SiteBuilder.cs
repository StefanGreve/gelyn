using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Gelyn.Components;
using Gelyn.Core;
using Gelyn.Core.Internals;
using Gelyn.Extensions;
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
///             <description>
///                 <see cref="WritePagesAsync"/> composes and writes the rendered pages.
///             </description>
///         </item>
///         <item>
///             <description>
///                 <see cref="CopyAssets"/> copies every located file that is not a page.
///             </description>
///         </item>
///         <item>
///             <description>
///                 <see cref="Reconcile"/> deletes the output that no located file accounts for.
///             </description>
///         </item>
///     </list>
/// </remarks>
internal sealed class SiteBuilder
{
    private readonly ContentPipeline _pipeline;
    private readonly IHostEnvironment _environment;
    private readonly IOptionsMonitor<SiteOptions> _options;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<SiteBuilder> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SiteBuilder"/> class.
    /// </summary>
    /// <param name="pipeline">
    ///     Discovers the files the site is built from, and renders the Markdown among them.
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
    /// <param name="logger">
    ///     Receives the records the build writes.
    /// </param>
    public SiteBuilder(
        ContentPipeline pipeline,
        IHostEnvironment environment,
        IOptionsMonitor<SiteOptions> options,
        IFileSystem fileSystem,
        ILogger<SiteBuilder> logger)
    {
        this._pipeline = pipeline;
        this._environment = environment;
        this._options = options;
        this._fileSystem = fileSystem;
        this._logger = logger;
    }

    /// <summary>
    ///     Generates the whole site.
    /// </summary>
    /// <param name="cancellationToken">
    ///     Token used to cancel the operation.
    /// </param>
    /// <returns>
    ///     The files that were written, the files that were copied, the files that were deleted, and how long
    ///     all of it took.
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

        long removeStarted = isMeasuring ? Stopwatch.GetTimestamp() : 0;
        List<string> removed = this.Reconcile(items, outputDirectory, cancellationToken);
        long removeTicks = isMeasuring ? Stopwatch.GetTimestamp() - removeStarted : 0;

        // Taken before the reports below, whose console writes would otherwise be counted in the build total.
        TimeSpan elapsed = Stopwatch.GetElapsedTime(started);

        if (isMeasuring)
        {
            this.ReportCounters(
                items.Count,
                pages.Count,
                written.Count + copied.Count,
                removed.Count,
                (scanTicks, renderTicks, composeTicks, writeTicks, removeTicks),
                elapsed);
        }

        return new BuildReport
        {
            Pages = written,
            Copies = copied,
            Removed = removed,
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

        var written = new List<string>(pages.Count);
        long composeTicks = 0;
        long writeTicks = 0;

        // The banner differs only in which navigation entry carries aria-current, so it needs one render per
        // linked page plus one shared by every page the navigation leaves out, however many pages there are.
        Dictionary<string, string> banners = new(navigation.Count + 1, StringComparer.Ordinal);

        foreach (ContentPage page in pages)
        {
            RenderContext context = page.ToRenderContext(options, navigation);

            long composeStarted = isMeasuring ? Stopwatch.GetTimestamp() : 0;
            string html = ComposeDocument(context, banners);

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

    private static string ComposeDocument(RenderContext context, Dictionary<string, string> banners)
    {
        string key = context.Page.InNavigation ? context.Page.Href : string.Empty;

        if (!banners.TryGetValue(key, out string? header))
        {
            header = HeaderComponent.Render(context);
            banners[key] = header;
        }

        // NOTE: the footer varies only by the page's date, so it could be cached per date.
        // Only warranted once the footer grows past the few AppendLine calls it costs today.
        return PageLayout.Render(context, header, FooterComponent.Render(context));
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

    private List<string> Reconcile(
        IReadOnlyList<ContentItem> items,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        HashSet<string> published = new(items.Count, StringComparer.Ordinal);

        foreach (ContentItem item in items)
            published.Add(item.OutputPath);

        List<string> removed = [];

        this.PruneDirectory(
            this._fileSystem.DirectoryInfo.New(outputDirectory),
            prefix: [],
            published,
            removed,
            cancellationToken);

        return removed;
    }

    /// <summary>
    ///     Deletes every file in one directory, and in the directories below it, that the scan does not
    ///     account for, together with the directories those deletions leave empty.
    /// </summary>
    /// <remarks>
    ///     Recursive like <see cref="ContentPipeline.CollectItems"/> rather than a flat enumeration, so a
    ///     skipped directory is never descended into and a deployed <c>.git</c> keeps its contents.
    /// </remarks>
    /// <param name="directory">
    ///     The directory to walk.
    /// </param>
    /// <param name="prefix">
    ///     The output-relative segments leading to <paramref name="directory"/>, empty at the output root.
    /// </param>
    /// <param name="published">
    ///     The output-relative path of every file the site consists of.
    /// </param>
    /// <param name="removed">
    ///     Receives the output-relative path of each file that was deleted.
    /// </param>
    /// <param name="cancellationToken">
    ///     Token used to cancel the operation.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> when anything in the directory survived, which is what tells the caller the
    ///     directory itself is not to be deleted.
    /// </returns>
    private bool PruneDirectory(
        IDirectoryInfo directory,
        string[] prefix,
        HashSet<string> published,
        List<string> removed,
        CancellationToken cancellationToken)
    {
        // Materialized ahead of the first delete. POSIX leaves it unspecified whether a directory stream
        // still returns an entry removed after the stream was opened, and not every file system keeps the
        // rest of the iteration stable: https://pubs.opengroup.org/onlinepubs/9799919799/functions/readdir.html
        IFileSystemInfo[] entries = [.. directory.EnumerateFileSystemInfos()];
        bool occupied = false;

        foreach (IFileSystemInfo entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // A dotfile belongs to the deploy target, and deleting through a link would reach past the output root.
            if (entry.Name.StartsWith('.') || entry.LinkTarget is not null)
            {
                occupied = true;
                continue;
            }

            string[] segments = [.. prefix, entry.Name];

            if (entry is IDirectoryInfo child)
            {
                if (this.PruneDirectory(child, segments, published, removed, cancellationToken))
                {
                    occupied = true;
                    continue;
                }

                child.Delete();
                continue;
            }

            string relativePath = this._fileSystem.Path.Combine(segments);

            if (published.Contains(relativePath))
            {
                occupied = true;
                continue;
            }

            entry.Delete();
            removed.Add(relativePath);
        }

        return occupied;
    }

    private void ReportCounters(
        int itemCount,
        int pageCount,
        int fileCount,
        int removedCount,
        (long Scan, long Render, long Compose, long Write, long Remove) ticks,
        TimeSpan elapsed)
    {
        this._logger.LogScanCompleted(itemCount, ticks.Scan.TotalMilliseconds);
        this._logger.LogRenderCompleted(pageCount, ticks.Render.TotalMilliseconds);
        this._logger.LogComposeCompleted(pageCount, ticks.Compose.TotalMilliseconds);
        this._logger.LogWriteCompleted(fileCount, ticks.Write.TotalMilliseconds);
        this._logger.LogRemoveCompleted(removedCount, ticks.Remove.TotalMilliseconds);
        this._logger.LogBuildCompleted(fileCount + removedCount, elapsed.TotalMilliseconds);
    }

    #endregion
}
