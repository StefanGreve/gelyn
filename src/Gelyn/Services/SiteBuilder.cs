using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO.Abstractions;

using Gelyn.Components;
using Gelyn.Core;
using Gelyn.Core.Context;
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
///                 <see cref="CreateOutputDirectories"/> creates every directory the located files land in.
///             </description>
///         </item>
///         <item>
///             <description>
///                 <see cref="WritePages"/> composes and writes the rendered pages.
///             </description>
///         </item>
///         <item>
///             <description>
///                 <see cref="WriteGenerated"/> writes the feed and the sitemap, each of which the
///                 configuration has to ask for.
///             </description>
///         </item>
///         <item>
///             <description>
///                 <see cref="CopyAssets"/> copies every located file that is not a page.
///             </description>
///         </item>
///         <item>
///             <description>
///                 <see cref="PruneOutput"/> deletes the output that no located file accounts for.
///             </description>
///         </item>
///     </list>
///     Pages are written and files copied on several threads through <see cref="OrderedParallel"/>. The report
///     still lists them in input order, and a failure surfaces as the one a sequential pass would have stopped
///     at.
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
    ///     What the build produced, and how long it took.
    /// </returns>
    /// <remarks>
    ///     The build runs on a thread pool thread, because its passes over the pages block until every page
    ///     is done.
    /// </remarks>
    public Task<BuildReport> BuildAsync(CancellationToken cancellationToken) =>
        Task.Run(() => this.Build(cancellationToken), CancellationToken.None);

    #region Helpers

    private BuildReport Build(CancellationToken cancellationToken)
    {
        SiteOptions options = this._options.CurrentValue;
        long started = Stopwatch.GetTimestamp();

        // The total is reported either way, so only the per-page detail is worth guarding.
        bool isMeasuring = this._logger.IsEnabled(LogLevel.Debug);

        IReadOnlyList<ContentItem> items = this._pipeline.Scan(options);
        long scanTicks = isMeasuring ? Stopwatch.GetTimestamp() - started : 0;

        IReadOnlyList<ContentPage> pages = this._pipeline.Render(items, cancellationToken);

        long renderTicks = isMeasuring ? Stopwatch.GetTimestamp() - started - scanTicks : 0;

        string outputDirectory = this._fileSystem.Path.Combine(
            this._environment.ContentRootPath,
            options.OutputDirectory);

        long outputStarted = isMeasuring ? Stopwatch.GetTimestamp() : 0;
        this.CreateOutputDirectories(items, outputDirectory);

        PageWriteContext write = this.WritePages(pages, options, outputDirectory, isMeasuring, cancellationToken);
        List<string> generated = this.WriteGenerated(pages, options, outputDirectory);
        string[] copied = this.CopyAssets(items, outputDirectory, cancellationToken);

        // Everything that reached the output directory counts as writing, except the share spent composing.
        long writeTicks = isMeasuring ? Stopwatch.GetTimestamp() - outputStarted - write.ComposeTicks : 0;

        long removeStarted = isMeasuring ? Stopwatch.GetTimestamp() : 0;
        Collection<string> removed = this.PruneOutput(items, generated, outputDirectory, cancellationToken);
        long removeTicks = isMeasuring ? Stopwatch.GetTimestamp() - removeStarted : 0;

        // Taken before the reports below, whose console writes would otherwise be counted in the build total.
        TimeSpan elapsed = Stopwatch.GetElapsedTime(started);

        if (isMeasuring)
        {
            this.ReportCounters(new BuildCounterContext
            {
                ItemCount = items.Count,
                PageCount = pages.Count,
                FileCount = write.Written.Count + generated.Count + copied.Length,
                RemovedCount = removed.Count,
                ScanTicks = scanTicks,
                RenderTicks = renderTicks,
                ComposeTicks = write.ComposeTicks,
                WriteTicks = writeTicks,
                RemoveTicks = removeTicks,
                Elapsed = elapsed,
            });
        }

        return new BuildReport
        {
            Pages = write.Written,
            Generated = generated,
            Copies = copied,
            Removed = removed,
            Elapsed = elapsed,
        };
    }

    // CreateDirectory probes the file system even for a directory that exists, so every directory is created
    // once. Creating all of them ahead of the parallel writes also leaves the writes nothing to coordinate.
    private void CreateOutputDirectories(IReadOnlyList<ContentItem> items, string outputDirectory)
    {
        IPath path = this._fileSystem.Path;
        IDirectory directory = this._fileSystem.Directory;

        // The root is created first, because the generated documents land there whether any item does.
        var created = new HashSet<string>(StringComparer.Ordinal) { outputDirectory };
        directory.CreateDirectory(outputDirectory);

        foreach (ContentItem item in items)
        {
            string target = path.GetDirectoryName(path.Combine(outputDirectory, item.OutputPath)) ?? outputDirectory;

            if (created.Add(target))
                directory.CreateDirectory(target);
        }
    }

    private PageWriteContext WritePages(
        IReadOnlyList<ContentPage> pages,
        SiteOptions options,
        string outputDirectory,
        bool isMeasuring,
        CancellationToken cancellationToken)
    {
        IPath path = this._fileSystem.Path;
        IFile file = this._fileSystem.File;
        IReadOnlyList<ContentPage> navigation = [.. pages.Where(static page => page.InNavigation)];
        var documents = new DocumentCatalog(pages);
        RenderContext[] contexts = [.. pages.Select(page => page.ToRenderContext(options, navigation, documents))];

        // Rendered up front, so that the parallel pass only reads the two dictionaries.
        long started = isMeasuring ? Stopwatch.GetTimestamp() : 0;
        Dictionary<string, string> banners = RenderBanners(contexts, navigation.Count + 1);
        Dictionary<string, string> footers = RenderFooters(contexts);
        long sharedTicks = isMeasuring ? Stopwatch.GetTimestamp() - started : 0;

        long composeTicks = 0;
        long writeTicks = 0;

        string[] written = OrderedParallel.Select(contexts, context =>
        {
            long composeStarted = isMeasuring ? Stopwatch.GetTimestamp() : 0;
            string html = ComposeDocument(context, banners, footers);
            long writeStarted = isMeasuring ? Stopwatch.GetTimestamp() : 0;

            file.WriteAllText(path.Combine(outputDirectory, context.Page.OutputPath), html);

            if (isMeasuring)
            {
                Interlocked.Add(ref composeTicks, writeStarted - composeStarted);
                Interlocked.Add(ref writeTicks, Stopwatch.GetTimestamp() - writeStarted);
            }

            return context.Page.OutputPath;
        }, cancellationToken);

        long passTicks = isMeasuring ? Stopwatch.GetTimestamp() - started - sharedTicks : 0;

        return new PageWriteContext
        {
            Written = written,
            ComposeTicks = sharedTicks + Apportion(passTicks, composeTicks, composeTicks + writeTicks),
        };
    }

    // The banner differs only in which navigation entry carries aria-current, so it needs one render per
    // linked page plus one shared by every page the navigation leaves out, however many pages there are.
    private static Dictionary<string, string> RenderBanners(RenderContext[] contexts, int capacity)
    {
        var banners = new Dictionary<string, string>(capacity, StringComparer.Ordinal);

        foreach (RenderContext context in contexts)
        {
            string key = BannerKey(context.Page);

            if (!banners.ContainsKey(key))
                banners[key] = HeaderComponent.Render(context);
        }

        return banners;
    }

    // The footer differs only in the page's revision date, so it needs one render per distinct date plus
    // one shared by every undated page, which also bounds the scan for feed entries the footer makes.
    private static Dictionary<string, string> RenderFooters(RenderContext[] contexts)
    {
        var footers = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (RenderContext context in contexts)
        {
            string key = FooterKey(context.Page);

            if (!footers.ContainsKey(key))
                footers[key] = FooterComponent.Render(context);
        }

        return footers;
    }

    private static string BannerKey(ContentPage page) =>
        page.InNavigation ? page.Href : string.Empty;

    private static string FooterKey(ContentPage page) =>
        page.Revised?.ToString(DateFormat.Machine, Tool.Culture) ?? string.Empty;

    private static string ComposeDocument(
        RenderContext context,
        Dictionary<string, string> banners,
        Dictionary<string, string> footers) =>
        PageLayout.Render(context, banners[BannerKey(context.Page)], footers[FooterKey(context.Page)]);

    // Compose and write overlap across threads, so neither has a wall time of its own. The pass's wall time is
    // split in the ratio of the thread time each took, which keeps the phases adding up to the build total.
    private static long Apportion(long wallTicks, long partTicks, long wholeTicks) =>
        wholeTicks == 0 ? 0 : (long)((double)wallTicks * partTicks / wholeTicks);

    /// <summary>
    ///     Writes the documents that no source file of their own stands behind.
    /// </summary>
    /// <param name="pages">
    ///     Every page the site is built from, which is what both documents are assembled from.
    /// </param>
    /// <param name="options">
    ///     The site configuration, which switches each document on and supplies the origin both need.
    /// </param>
    /// <param name="outputDirectory">
    ///     The absolute path of the directory the documents are written to, which already exists.
    /// </param>
    /// <returns>
    ///     The output-relative paths of the documents that were written, which is what exempts them from
    ///     <see cref="PruneOutput"/>.
    /// </returns>
    private List<string> WriteGenerated(IReadOnlyList<ContentPage> pages, SiteOptions options, string outputDirectory)
    {
        List<string> generated = [];

        string? feed = this.WriteFeed(pages, options, outputDirectory);

        if (feed is not null) generated.Add(feed);

        string? sitemap = this.WriteSitemap(pages, options, outputDirectory);

        if (sitemap is not null) generated.Add(sitemap);

        return generated;
    }

    /// <summary>
    ///     Writes the syndication feed, or nothing at all when the configuration has not asked for one or
    ///     when the feed would carry no entry.
    /// </summary>
    /// <param name="pages">
    ///     Every page the site is built from, of which the feed publishes those that declare it.
    /// </param>
    /// <param name="options">
    ///     The site configuration, which both switches the feed on and supplies the origin its links need.
    /// </param>
    /// <param name="outputDirectory">
    ///     The absolute path of the directory the feed is written to, which already exists.
    /// </param>
    /// <returns>
    ///     The output-relative path of the feed, or <see langword="null"/> when none was written.
    /// </returns>
    private string? WriteFeed(IReadOnlyList<ContentPage> pages, SiteOptions options, string outputDirectory)
    {
        if (!options.EnableFeed) return null;

        // Reported ahead of the render, which answers with nothing for either reason and so cannot tell the
        // two apart on its own.
        if (options.BaseUrl is null)
        {
            this._logger.LogFeedSkippedWithoutBaseUrl();
            return null;
        }

        // The one conflict the opt-in makes expressible: a page asks to be published and declares no date to
        // be published under, which Atom requires of every entry.
        foreach (ContentPage page in pages.Where(static page => page.InFeed && page.Date is null))
            this._logger.LogFeedEntrySkipped(page.SourcePath);

        byte[] document = AtomFeed.Render(options, pages);

        if (document.Length == 0)
        {
            this._logger.LogFeedSkippedWithoutEntries();
            return null;
        }

        this._fileSystem.File.WriteAllBytes(this._fileSystem.Path.Combine(outputDirectory, AtomFeed.FileName), document);

        return AtomFeed.FileName;
    }

    /// <summary>
    ///     Writes the sitemap, or nothing at all when the configuration has not asked for one or declares no
    ///     base URL to resolve its locations against.
    /// </summary>
    /// <param name="pages">
    ///     Every page the site is built from, all of which the sitemap lists.
    /// </param>
    /// <param name="options">
    ///     The site configuration, which both switches the sitemap on and supplies the origin its locations
    ///     need.
    /// </param>
    /// <param name="outputDirectory">
    ///     The absolute path of the directory the sitemap is written to, which already exists.
    /// </param>
    /// <returns>
    ///     The output-relative path of the sitemap, or <see langword="null"/> when none was written.
    /// </returns>
    private string? WriteSitemap(IReadOnlyList<ContentPage> pages, SiteOptions options, string outputDirectory)
    {
        if (!options.EnableSitemap) return null;

        // Checked here too, the render answering with nothing for this reason and holding no logger to say so.
        if (options.BaseUrl is null)
        {
            this._logger.LogSitemapSkippedWithoutBaseUrl();
            return null;
        }

        // Always a document from here on. The landing page is always listed, and the scan fails a tree without one.
        byte[] document = Sitemap.Render(options, pages);

        this._fileSystem.File.WriteAllBytes(this._fileSystem.Path.Combine(outputDirectory, Sitemap.FileName), document);

        return Sitemap.FileName;
    }

    private string[] CopyAssets(
        IReadOnlyList<ContentItem> items,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        IPath path = this._fileSystem.Path;
        IFile file = this._fileSystem.File;
        ContentItem[] assets = [.. items.Where(static item => item.Kind is not ContentKind.Page)];

        return OrderedParallel.Select(assets, asset =>
        {
            file.Copy(asset.SourcePath, path.Combine(outputDirectory, asset.OutputPath), overwrite: true);

            return asset.OutputPath;
        }, cancellationToken);
    }

    private Collection<string> PruneOutput(
        IReadOnlyList<ContentItem> items,
        List<string> generated,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        var published = new HashSet<string>(items.Count + generated.Count, StringComparer.Ordinal);

        foreach (ContentItem item in items)
            published.Add(item.OutputPath);

        foreach (string path in generated)
            published.Add(path);

        var context = new PruneContext
        {
            Prefix = [],
            Published = published,
            Removed = [],
        };

        this.PruneDirectory(this._fileSystem.DirectoryInfo.New(outputDirectory), context, cancellationToken);

        return context.Removed;
    }

    /// <summary>
    ///     Deletes every file in one directory, and in the directories below it, that the scan does not
    ///     account for, together with the directories those deletions leave empty.
    /// </summary>
    /// <remarks>
    ///     Recursive like <see cref="ContentPipeline.CollectItems"/>, because a flat enumeration would descend
    ///     into a skipped directory and cost a deployed <c>.git</c> its contents.
    /// </remarks>
    /// <param name="directory">
    ///     The directory to walk.
    /// </param>
    /// <param name="context">
    ///     What this level of the walk carries, deletions included.
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
        PruneContext context,
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
            if (entry.Name.StartsWith('.') || entry.IsLink)
            {
                occupied = true;
                continue;
            }

            string[] segments = [.. context.Prefix, entry.Name];

            if (entry is IDirectoryInfo child)
            {
                if (this.PruneDirectory(child, context with { Prefix = segments }, cancellationToken))
                {
                    occupied = true;
                    continue;
                }

                child.Delete();
                continue;
            }

            string relativePath = this._fileSystem.Path.Combine(segments);

            if (context.Published.Contains(relativePath))
            {
                occupied = true;
                continue;
            }

            entry.Delete();
            context.Removed.Add(relativePath);
        }

        return occupied;
    }

    private void ReportCounters(BuildCounterContext counters)
    {
        this._logger.LogScanCompleted(counters.ItemCount, counters.ScanTicks.TotalMilliseconds);
        this._logger.LogRenderCompleted(counters.PageCount, counters.RenderTicks.TotalMilliseconds);
        this._logger.LogComposeCompleted(counters.PageCount, counters.ComposeTicks.TotalMilliseconds);
        this._logger.LogWriteCompleted(counters.FileCount, counters.WriteTicks.TotalMilliseconds);
        this._logger.LogRemoveCompleted(counters.RemovedCount, counters.RemoveTicks.TotalMilliseconds);
        this._logger.LogBuildCompleted(counters.FileCount + counters.RemovedCount, counters.Elapsed.TotalMilliseconds);
    }

    #endregion
}
