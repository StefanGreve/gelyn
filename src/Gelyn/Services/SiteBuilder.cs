using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO.Abstractions;

using Gelyn.Abstractions;
using Gelyn.Components;
using Gelyn.Core;
using Gelyn.Core.Context;
using Gelyn.Core.Internals;
using Gelyn.Diagnostics;
using Gelyn.Extensions;
using Gelyn.Model;
using Gelyn.Model.Options;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gelyn.Services;

/// <summary>
///     <inheritdoc path="/summary/node()"/>
/// </summary>
/// <remarks>
///     The build is processed in this order:
///     <list type="number">
///         <item>
///             <description>
///                 <see cref="ComposeGenerated"/> composes the feed, the sitemap and the files of the theme,
///                 each of which the configuration has to ask for.
///             </description>
///         </item>
///         <item>
///             <description>
///                 <see cref="PruneOutput"/> deletes the output the build no longer publishes, before anything
///                 is written.
///             </description>
///         </item>
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
///                 <see cref="WriteGenerated"/> writes the composed documents.
///             </description>
///         </item>
///         <item>
///             <description>
///                 <see cref="CopyAssets"/> copies every located file that is not a page.
///             </description>
///         </item>
///     </list>
///     Pages are written and files copied on several threads through <see cref="OrderedParallel"/>. The report
///     still lists them in input order, and a failure surfaces as the one a sequential pass would have stopped
///     at.
/// </remarks>
internal sealed class SiteBuilder : SiteBuilderContract
{
    private readonly ContentPipelineContract _pipeline;
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
        ContentPipelineContract pipeline,
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

    /// <inheritdoc/>
    /// <remarks>
    ///     The build runs on a thread pool thread, because its passes over the pages block until every page
    ///     is done.
    /// </remarks>
    public override Task<BuildReport> BuildAsync(CancellationToken cancellationToken) =>
        Task.Run(() => this.Build(cancellationToken), CancellationToken.None);

    /// <summary>
    ///     Writes one counter per phase, in the order the phases run, followed by the heap figures and the build
    ///     total.
    /// </summary>
    /// <remarks>
    ///     The seam a test drives with durations of its own choosing. A build cannot supply them, because its
    ///     phases are timed with <see cref="Stopwatch.GetTimestamp"/>, so only distinct values set by hand show
    ///     that no counter reports the duration of another phase.
    /// </remarks>
    /// <param name="counters">
    ///     What the build counted and how long each of its phases took.
    /// </param>
    internal void ReportCounters(BuildCounterContext counters)
    {
        ArgumentNullException.ThrowIfNull(counters);

        this._logger.LogScanCompleted(counters.ItemCount, counters.ScanTicks.TotalMilliseconds);
        this._logger.LogRenderCompleted(counters.PageCount, counters.RenderTicks.TotalMilliseconds);
        this._logger.LogRemoveCompleted(counters.RemovedCount, counters.RemoveTicks.TotalMilliseconds);
        this._logger.LogComposeCompleted(counters.PageCount, counters.ComposeTicks.TotalMilliseconds);
        this._logger.LogWriteCompleted(counters.FileCount, counters.WriteTicks.TotalMilliseconds);

        GarbageCollectionContext heap = counters.GarbageCollection;

        this._logger.LogGarbageCollected(
            heap.AllocatedBytes / (1024d * 1024d),
            heap.Pause.TotalMilliseconds,
            heap.Gen0Count,
            heap.Gen1Count,
            heap.Gen2Count);

        this._logger.LogBuildCompleted(counters.FileCount + counters.RemovedCount, counters.Elapsed.TotalMilliseconds);
    }

    #region Helpers

    private BuildReport Build(CancellationToken cancellationToken)
    {
        SiteOptions options = this._options.CurrentValue;

        // The total is reported either way, so only the per-page detail and the heap snapshots are guarded.
        var clock = BuildClock.Start(this._logger.IsEnabled(LogLevel.Debug));

        IReadOnlyList<ContentItem> items = this._pipeline.Scan(options);
        clock.Charge(BuildPhase.Scan);

        IReadOnlyList<ContentPage> pages = this._pipeline.Render(items, cancellationToken);
        clock.Charge(BuildPhase.Render);

        foreach (ContentPage page in pages)
            foreach (UnknownKey key in page.UnknownKeys)
                this._logger.LogFrontMatterKeyIgnored(key.Name, key.Line, page.SourcePath);

        string outputDirectory = this._fileSystem.Path.Combine(
            this._environment.ContentRootPath,
            options.OutputDirectory);

        // Writing through a link is dangerous, since the prune deletes any data at its target the build did not write.
        if (this._fileSystem.DirectoryInfo.New(outputDirectory).IsLink)
            throw new IOException($"The output directory '{outputDirectory}' is a link, which a build does not write through.");

        clock.Skip();
        List<GeneratedDocument> composed = this.ComposeGenerated(items, pages, options);
        clock.Charge(BuildPhase.Write);

        // Ahead of every write, because a file system that ignores case keeps the spelling of a file a write
        // replaces, which the ordinal comparison would then take for a leftover and delete.
        Collection<string> removed = this.PruneOutput(items, composed, outputDirectory, cancellationToken);
        clock.Charge(BuildPhase.Remove);

        this.CreateOutputDirectories(items, outputDirectory);

        string[] written = this.WritePages(pages, options, outputDirectory, clock, cancellationToken);
        string[] generated = this.WriteGenerated(composed, outputDirectory);
        string[] copied = this.CopyAssets(items, outputDirectory, cancellationToken);
        clock.Charge(BuildPhase.Write);

        // Taken before the reports below, whose console writes would otherwise be counted in the build total.
        TimeSpan elapsed = clock.Stop();

        if (clock.IsMeasuring)
            this.ReportCounters(clock.CollectCounters(items.Count, pages.Count, written.Length + generated.Length + copied.Length, removed.Count));

        return new BuildReport
        {
            Pages = written,
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

    private string[] WritePages(
        IReadOnlyList<ContentPage> pages,
        SiteOptions options,
        string outputDirectory,
        BuildClock clock,
        CancellationToken cancellationToken)
    {
        IPath path = this._fileSystem.Path;
        IReadOnlyList<ContentPage> navigation = [.. pages.Where(static page => page.InNavigation)];
        var documents = new DocumentCatalog(pages);
        RenderContext[] contexts = [.. pages.Select(page => page.ToRenderContext(options, navigation, documents))];
        clock.Charge(BuildPhase.Write);

        // Rendered up front, so that the parallel pass only reads the two dictionaries.
        Dictionary<string, string> banners = RenderBanners(contexts, navigation.Count + 1);
        Dictionary<string, string> footers = RenderFooters(contexts);
        clock.Charge(BuildPhase.Compose);

        bool isMeasuring = clock.IsMeasuring;
        long composeTicks = 0;
        long writeTicks = 0;

        string[] written = OrderedParallel.Select(contexts, context =>
        {
            long composeStarted = isMeasuring ? Stopwatch.GetTimestamp() : 0;
            string html = ComposeDocument(context, banners, footers);
            long writeStarted = isMeasuring ? Stopwatch.GetTimestamp() : 0;

            FileUtils.WriteUtf8(this._fileSystem, path.Combine(outputDirectory, context.Page.OutputPath), html);

            if (isMeasuring)
            {
                Interlocked.Add(ref composeTicks, writeStarted - composeStarted);
                Interlocked.Add(ref writeTicks, Stopwatch.GetTimestamp() - writeStarted);
            }

            return context.Page.OutputPath;
        }, cancellationToken);

        clock.Split(composeTicks, writeTicks);

        return written;
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
    // one shared by every undated page.
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
        StringUtils.NormalizeLineEndings(PageLayout.Render(context, banners[BannerKey(context.Page)], footers[FooterKey(context.Page)]));

    /// <summary>
    ///     Composes the documents that no source file of their own stands behind: the feed, the sitemap and the
    ///     files of the theme.
    /// </summary>
    /// <param name="items">
    ///     Every file the scan located, among which a file replacing one of the theme's is found.
    /// </param>
    /// <param name="pages">
    ///     Every page the site is built from, which is what the feed and the sitemap are assembled from.
    /// </param>
    /// <param name="options">
    ///     The site configuration, which switches each document on, supplies the origin the feed and the
    ///     sitemap need, and names the theme.
    /// </param>
    /// <returns>
    ///     The documents to write, whose paths are what exempts them from <see cref="PruneOutput"/>.
    /// </returns>
    private List<GeneratedDocument> ComposeGenerated(
        IReadOnlyList<ContentItem> items,
        IReadOnlyList<ContentPage> pages,
        SiteOptions options)
    {
        List<GeneratedDocument> composed = [];

        if (this.ComposeFeed(pages, options) is byte[] feed)
            composed.Add(new GeneratedDocument { Path = AtomFeed.FileName, Contents = feed });

        if (this.ComposeSitemap(pages, options) is byte[] sitemap)
            composed.Add(new GeneratedDocument { Path = Sitemap.FileName, Contents = sitemap });

        composed.AddRange(this.CollectThemeFiles(items, options));

        return composed;
    }

    /// <summary>
    ///     Composes the syndication feed, or nothing at all when the configuration has not asked for one or
    ///     when the feed would carry no entry.
    /// </summary>
    /// <param name="pages">
    ///     Every page the site is built from, of which the feed publishes those that declare it.
    /// </param>
    /// <param name="options">
    ///     The site configuration, which both switches the feed on and supplies the origin its links need.
    /// </param>
    /// <returns>
    ///     The document, or <see langword="null"/> when there is none to write.
    /// </returns>
    private byte[]? ComposeFeed(IReadOnlyList<ContentPage> pages, SiteOptions options)
    {
        if (!options.EnableFeed) return null;

        if (options.BaseUrl is not Uri baseUrl)
        {
            this._logger.LogFeedSkippedWithoutBaseUrl();
            return null;
        }

        // The one conflict the opt-in makes expressible: a page asks to be published and declares no date to
        // be published under, which Atom requires of every entry.
        foreach (ContentPage page in pages.Where(static page => page.InFeed && page.Date is null))
            this._logger.LogFeedEntrySkipped(page.SourcePath);

        if (AtomFeed.Render(options, baseUrl, pages) is not byte[] document)
        {
            this._logger.LogFeedSkippedWithoutEntries();
            return null;
        }

        return document;
    }

    /// <summary>
    ///     Composes the sitemap, or nothing at all when the configuration has not asked for one or declares no
    ///     base URL to resolve its locations against.
    /// </summary>
    /// <param name="pages">
    ///     Every page the site is built from, all of which the sitemap lists.
    /// </param>
    /// <param name="options">
    ///     The site configuration, which both switches the sitemap on and supplies the origin its locations
    ///     need.
    /// </param>
    /// <returns>
    ///     The document, or <see langword="null"/> when there is none to write.
    /// </returns>
    private byte[]? ComposeSitemap(IReadOnlyList<ContentPage> pages, SiteOptions options)
    {
        if (!options.EnableSitemap) return null;

        if (options.BaseUrl is not Uri baseUrl)
        {
            this._logger.LogSitemapSkippedWithoutBaseUrl();
            return null;
        }

        // Always a document from here on. The landing page is always listed, and the scan fails a tree without one.
        (byte[] document, int locations) = Sitemap.Render(baseUrl, pages);

        // Splitting the sitemap into an index is not supported yet, so a site past a limit learns of it here.
        if (Sitemap.ExceedsLimits(locations, document.Length))
            this._logger.LogSitemapLimitExceeded(locations, document.Length);

        return document;
    }

    // A file at the root of the content directory replaces the theme's file of the same name, which is how an edit
    // to a theme is previewed: gelyn serve rebuilds on a change to the content, never on one to the tool.
    private List<GeneratedDocument> CollectThemeFiles(IReadOnlyList<ContentItem> items, SiteOptions options)
    {
        List<GeneratedDocument> collected = [];

        if (options.Theme is not string theme) return collected;

        foreach (ThemeFile file in ThemeCatalog.Files(theme))
        {
            ContentItem? replacement = items.FirstOrDefault(item => item.OutputPath.Equals(file.FileName, StringComparison.Ordinal));

            if (replacement is not null)
            {
                this._logger.LogThemeFileReplaced(file.FileName, theme, replacement.SourcePath);
                continue;
            }

            collected.Add(new GeneratedDocument
            {
                Path = file.FileName,
                Contents = ThemeCatalog.Read(file)
            });
        }

        return collected;
    }

    private string[] WriteGenerated(List<GeneratedDocument> composed, string outputDirectory)
    {
        foreach (GeneratedDocument document in composed)
            this._fileSystem.File.WriteAllBytes(this._fileSystem.Path.Combine(outputDirectory, document.Path), document.Contents.Span);

        return [.. composed.Select(static document => document.Path)];
    }

    private string[] CopyAssets(
        IReadOnlyList<ContentItem> items,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        IPath path = this._fileSystem.Path;
        ContentItem[] assets = [.. items.Where(static item => item.Kind is not ContentKind.Page)];

        return OrderedParallel.Select(assets, asset =>
        {
            FileUtils.CopyContent(this._fileSystem, asset.SourcePath, path.Combine(outputDirectory, asset.OutputPath));

            return asset.OutputPath;
        }, cancellationToken);
    }

    private Collection<string> PruneOutput(
        IReadOnlyList<ContentItem> items,
        List<GeneratedDocument> composed,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        IDirectoryInfo root = this._fileSystem.DirectoryInfo.New(outputDirectory);

        // Created after the prune, so the first build finds nothing to prune.
        if (!root.Exists) return [];

        var published = new HashSet<string>(items.Count + composed.Count, StringComparer.Ordinal);

        foreach (ContentItem item in items)
            published.Add(item.OutputPath);

        foreach (GeneratedDocument document in composed)
            published.Add(document.Path);

        var context = new PruneContext
        {
            Prefix = [],
            Published = published,
            PublishedDirectories = this.CollectPublishedDirectories(published),
            Removed = [],
        };

        this.PruneDirectory(root, context, cancellationToken);

        return context.Removed;
    }

    // Keyed without regard to case, so that a directory kept under another spelling finds the one it is published under.
    private Dictionary<string, string> CollectPublishedDirectories(HashSet<string> published)
    {
        IPath path = this._fileSystem.Path;
        var directories = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (string file in published)
        {
            for (string? directory = path.GetDirectoryName(file); !string.IsNullOrEmpty(directory); directory = path.GetDirectoryName(directory))
            {
                // Present already, and so are its ancestors. The scan admits one spelling per directory.
                if (!directories.TryAdd(directory, directory)) break;
            }
        }

        return directories;
    }

    /// <summary>
    ///     Deletes every file in one directory, and in the directories below it, that the build does not
    ///     publish, together with the directories those deletions leave empty, and gives a directory that survives
    ///     under another spelling the one it is published under.
    /// </summary>
    /// <remarks>
    ///     Recursive like the scan, because a flat enumeration would descend into a skipped directory and
    ///     cost a deployed <c>.git</c> its contents.
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
        // Sorted like the scan, so that the removed lines read the same on every file system.
        IFileSystemInfo[] entries = [.. directory.EnumerateFileSystemInfos().OrderBy(static entry => entry.Name, StringComparer.Ordinal)];
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
                    this.AdoptPublishedSpelling(directory, child, segments, entries, context.PublishedDirectories);
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

    // A dot entry keeps a directory from being pruned, so after a rename in case alone, such as Blog to blog, it keeps
    // its old name, and a file system that ignores case writes the new pages into it. Renaming it fixes the spelling.
    private void AdoptPublishedSpelling(
        IDirectoryInfo parent,
        IDirectoryInfo child,
        string[] segments,
        IFileSystemInfo[] siblings,
        Dictionary<string, string> publishedDirectories)
    {
        IPath path = this._fileSystem.Path;
        string relativePath = path.Combine(segments);

        if (!publishedDirectories.TryGetValue(relativePath, out string? published)) return;

        // Every directory a build keeps for its pages is published under its own spelling, so only a leftover goes on.
        if (published.Equals(relativePath, StringComparison.Ordinal)) return;

        string name = path.GetFileName(published);
        string target = path.Combine(parent.FullName, name);

        // A file system that respects case lists the published spelling as an entry of its own or cannot resolve it,
        // while one that ignores case resolves it to this very directory.
        if (siblings.Any(sibling => sibling.Name.Equals(name, StringComparison.Ordinal)) || !this._fileSystem.Directory.Exists(target)) return;

        // Through an unused name, because Directory.Move refuses a destination that already exists, and the new
        // spelling still resolves to this directory.
        string transit = path.Combine(parent.FullName, path.GetRandomFileName());

        this._fileSystem.Directory.Move(child.FullName, transit);
        this._fileSystem.Directory.Move(transit, target);
    }

    #endregion
}
