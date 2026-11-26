using System.Diagnostics;
using System.IO.Abstractions;

using Gelyn.Core;
using Gelyn.Core.Context;
using Gelyn.Core.Internals;
using Gelyn.Diagnostics;
using Gelyn.Exceptions;
using Gelyn.Extensions;
using Gelyn.Model;
using Gelyn.Model.Options;

using Microsoft.Extensions.Logging;

using SharpYaml;

namespace Gelyn.Services;

/// <summary>
///     Writes the finished site to the output directory.
/// </summary>
internal sealed class SiteBuilder
{
    private readonly ContentPipeline _pipeline;
    private readonly string _contentRoot;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<SiteBuilder> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SiteBuilder"/> class.
    /// </summary>
    /// <param name="pipeline">
    ///     Discovers the files the site is built from, and renders the Markdown among them.
    /// </param>
    /// <param name="contentRoot">
    ///     The directory relative paths are resolved against.
    /// </param>
    /// <param name="fileSystem">
    ///     The file system to work against.
    /// </param>
    /// <param name="logger">
    ///     Receives the records the build writes.
    /// </param>
    public SiteBuilder(
        ContentPipeline pipeline,
        string contentRoot,
        IFileSystem fileSystem,
        ILogger<SiteBuilder> logger)
    {
        this._pipeline = pipeline;
        this._contentRoot = contentRoot;
        this._fileSystem = fileSystem;
        this._logger = logger;
    }

    /// <summary>
    ///     Generates the whole site.
    /// </summary>
    /// <param name="options">
    ///     The settings the site is built against.
    /// </param>
    /// <param name="cancellationToken">
    ///     Token used to cancel the operation.
    /// </param>
    /// <returns>
    ///     What the build produced, and how long it took.
    /// </returns>
    /// <exception cref="ConfigurationException">
    ///     The content directory publishes no landing page.
    /// </exception>
    /// <exception cref="YamlException">
    ///     A front matter block is not well-formed YAML.
    /// </exception>
    /// <exception cref="BuildException">
    ///     The output directory is a link, one path is published twice, or a front matter block declares a
    ///     stylesheet or a script the scan did not locate.
    /// </exception>
    /// <exception cref="IOException">
    ///     A file cannot be written or removed.
    /// </exception>
    public Task<BuildReport> BuildAsync(SiteOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        // The build runs on a thread pool thread, because its passes over the pages block until every page is done.
        return Task.Run(() => this.Build(options, cancellationToken), CancellationToken.None);
    }

    /// <summary>
    ///     Writes one counter per phase, in the order the phases run, followed by the heap figures and the build
    ///     total.
    /// </summary>
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

    private BuildReport Build(SiteOptions options, CancellationToken cancellationToken)
    {
        // The total is reported either way, so only the per-page detail and the heap snapshots are guarded.
        var clock = BuildClock.Start(this._logger.IsEnabled(LogLevel.Debug));

        IReadOnlyList<ContentItem> items = this._pipeline.Scan(options);
        clock.Charge(BuildPhase.Scan);

        IReadOnlyList<ContentPage> pages = this._pipeline.Render(items, cancellationToken);
        clock.Charge(BuildPhase.Render);

        foreach (ContentPage page in pages)
        {
            foreach (UnknownKey key in page.UnknownKeys)
                this._logger.LogFrontMatterKeyIgnored(key.Name, key.Line, page.SourcePath);

            foreach (InvalidValue value in page.InvalidValues)
                this._logger.LogFrontMatterValueIgnored(value.Name, value.Line, page.SourcePath, value.Message);
        }

        string outputDirectory = this._fileSystem.Path.Combine(this._contentRoot, options.OutputDirectory);

        // One entry for the link check and the prune's existence check, which share its cached attributes.
        IDirectoryInfo root = this._fileSystem.DirectoryInfo.New(outputDirectory);

        // Writing through a link is dangerous, since the prune deletes any data at its target the build did not write.
        if (root.IsLink)
            throw new BuildException($"The output directory '{outputDirectory}' is a link, which a build does not write through.");

        clock.Skip();
        List<GeneratedDocument> composed = this.ComposeGenerated(items, pages, options);
        clock.Charge(BuildPhase.Write);

        // Ahead of every write, because a file system that ignores case keeps the spelling of a file a write
        // replaces, which the ordinal comparison would then take for a leftover and delete.
        PublishedTree tree = this.CollectPublishedTree(items, composed);
        PruneContext pruned = this.PruneOutput(root, tree, cancellationToken);
        clock.Charge(BuildPhase.Remove);

        this.CreateMissingDirectories(tree, pruned.Missing, outputDirectory);

        string[] written = this.WriteDocuments(pages, composed, options, outputDirectory, clock, cancellationToken);
        string[] copied = this.CopyAssets(items, outputDirectory, cancellationToken);
        clock.Charge(BuildPhase.Write);

        // Taken before the reports below, whose console writes would otherwise be counted in the build total.
        TimeSpan elapsed = clock.Stop();

        if (clock.IsMeasuring)
            this.ReportCounters(clock.CollectCounters(items.Count, pages.Count, written.Length + copied.Length, pruned.Removed.Count));

        return new BuildReport
        {
            Pages = new ArraySegment<string>(written, 0, pages.Count),
            Generated = new ArraySegment<string>(written, pages.Count, composed.Count),
            Copies = copied,
            Removed = pruned.Removed,
            Elapsed = elapsed,
        };
    }

    private static string DescribeGenerated(string path) =>
        path switch
        {
            AtomFeed.FileName => "the generated feed",
            Sitemap.FileName => "the generated sitemap",
            _ => "a file of the theme",
        };

    // The root first, because the generated documents land there whether any item does, and every directory ahead of
    // those below it, so that creating the deepest missing ones creates the rest.
    private PublishedTree CollectPublishedTree(IReadOnlyList<ContentItem> items, List<GeneratedDocument> composed)
    {
        IPath path = this._fileSystem.Path;
        var files = new HashSet<string>(items.Count + composed.Count, StringComparer.Ordinal);
        var known = new HashSet<string>(StringComparer.Ordinal) { string.Empty };
        List<string> directories = [string.Empty];

        foreach (ContentItem item in items)
        {
            files.Add(item.OutputPath);

            int first = directories.Count;

            for (string? directory = path.GetDirectoryName(item.OutputPath); !string.IsNullOrEmpty(directory); directory = path.GetDirectoryName(directory))
            {
                // Present already, and so are its ancestors.
                if (!known.Add(directory)) break;

                directories.Add(directory);
            }

            // Walked up from the file, so the new ones are turned round into the order a creation needs.
            directories.Reverse(first, directories.Count - first);
        }

        foreach (GeneratedDocument document in composed)
        {
            if (items.FirstOrDefault(item => item.OutputPath.Equals(document.Path, StringComparison.OrdinalIgnoreCase)) is ContentItem item)
                throw new BuildException($"'{item.SourcePath}' and {DescribeGenerated(document.Path)} both publish '{document.Path}'.");

            files.Add(document.Path);
        }

        return new PublishedTree
        {
            Files = files,
            Directories = directories
        };
    }

    // CreateDirectory probes the file system even for a directory that exists, so only the directories the prune did
    // not find are created, and of those the deepest alone, since a call creates the ancestors its path needs.
    // Creating them ahead of the parallel writes also leaves the writes nothing to coordinate.
    private void CreateMissingDirectories(PublishedTree tree, HashSet<string> missing, string outputDirectory)
    {
        IPath path = this._fileSystem.Path;
        var parents = new HashSet<string>(StringComparer.Ordinal);

        foreach (string relativePath in missing)
            if (path.GetDirectoryName(relativePath) is string parent)
                parents.Add(parent);

        foreach (string relativePath in tree.Directories)
            if (missing.Contains(relativePath) && !parents.Contains(relativePath))
                this._fileSystem.Directory.CreateDirectory(path.Combine(outputDirectory, relativePath));
    }

    private string[] WriteDocuments(
        IReadOnlyList<ContentPage> pages,
        List<GeneratedDocument> composed,
        SiteOptions options,
        string outputDirectory,
        BuildClock clock,
        CancellationToken cancellationToken)
    {
        clock.Charge(BuildPhase.Write);

        // Rendered once, up front, so that the parallel pass only reads it.
        var layout = new LayoutContext(options, pages);
        clock.Charge(BuildPhase.Compose);

        bool isMeasuring = clock.IsMeasuring;
        long composeTicks = 0;
        long writeTicks = 0;

        string[] written = OrderedParallel.For(pages.Count + composed.Count, index =>
            index < pages.Count
                ? this.WritePage(layout, pages[index], outputDirectory, isMeasuring, ref composeTicks, ref writeTicks)
                : this.WriteGenerated(composed[index - pages.Count], outputDirectory, isMeasuring, ref writeTicks), cancellationToken);

        clock.Split(composeTicks, writeTicks);

        return written;
    }

    private string WritePage(LayoutContext layout, ContentPage page, string outputDirectory, bool isMeasuring, ref long composeTicks, ref long writeTicks)
    {
        long composeStarted = isMeasuring ? Stopwatch.GetTimestamp() : 0;
        using var document = new PageBuffer();
        PageLayout.Render(layout, page, document);
        long writeStarted = isMeasuring ? Stopwatch.GetTimestamp() : 0;

        this._fileSystem.File.WriteAllBytes(this._fileSystem.Path.Combine(outputDirectory, page.OutputPath), document.WrittenSpan);

        if (isMeasuring)
        {
            Interlocked.Add(ref composeTicks, writeStarted - composeStarted);
            Interlocked.Add(ref writeTicks, Stopwatch.GetTimestamp() - writeStarted);
        }

        return page.OutputPath;
    }

    private string WriteGenerated(GeneratedDocument document, string outputDirectory, bool isMeasuring, ref long writeTicks)
    {
        long started = isMeasuring ? Stopwatch.GetTimestamp() : 0;

        this._fileSystem.File.WriteAllBytes(this._fileSystem.Path.Combine(outputDirectory, document.Path), document.Contents.Span);

        if (isMeasuring) Interlocked.Add(ref writeTicks, Stopwatch.GetTimestamp() - started);

        return document.Path;
    }

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

    private string[] CopyAssets(
        IReadOnlyList<ContentItem> items,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        IPath path = this._fileSystem.Path;
        ContentItem[] assets = [.. items.Where(static item => item.Kind is not ContentKind.Page)];

        return OrderedParallel.Select(assets, asset =>
        {
            this._fileSystem.File.CopyContent(asset.SourcePath, path.Combine(outputDirectory, asset.OutputPath), asset.SourceLastWriteTimeUtc);

            return asset.OutputPath;
        }, cancellationToken);
    }

    private PruneContext PruneOutput(IDirectoryInfo root, PublishedTree tree, CancellationToken cancellationToken)
    {
        var context = new PruneContext
        {
            Published = tree.Files,
            PublishedDirectories = new HashSet<string>(tree.Directories, StringComparer.OrdinalIgnoreCase),
            Missing = new HashSet<string>(tree.Directories, StringComparer.Ordinal),
            Removed = [],
        };

        // Created after the prune, so the first build finds nothing to prune and every directory missing.
        if (!root.Exists) return context;

        // The walk never deletes the root, so it is the one directory that is present without being confirmed.
        context.Missing.Remove(string.Empty);
        this.PruneDirectory(root, string.Empty, context, cancellationToken);

        return context;
    }

    /// <summary>
    ///     Deletes every file in one directory, and in the directories below it, that the build does not
    ///     publish, together with the directories those deletions leave empty, gives a directory that survives
    ///     under another spelling the one it is published under, and strikes every directory found under its
    ///     published spelling off the missing ones.
    /// </summary>
    /// <param name="directory">
    ///     The directory to walk.
    /// </param>
    /// <param name="relativePath">
    ///     The output-relative path of <paramref name="directory"/>, empty at the output root.
    /// </param>
    /// <param name="context">
    ///     What the walk carries, deletions and missing directories included.
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
        string relativePath,
        PruneContext context,
        CancellationToken cancellationToken)
    {
        IPath path = this._fileSystem.Path;

        IFileSystemInfo[] entries = directory.ListEntries();
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

            string entryPath = path.Combine(relativePath, entry.Name);

            if (entry is IDirectoryInfo child)
            {
                // Recursive, since a flat enumeration would reach into a skipped directory and empty a deployed .git.
                if (this.PruneDirectory(child, entryPath, context, cancellationToken))
                {
                    // Found under its published spelling, as every directory a build keeps for its pages is, or else
                    // under the spelling the rename leaves it with.
                    if (!context.Missing.Remove(entryPath))
                        context.Missing.Remove(this.AdoptPublishedSpelling(directory, child, entryPath, entries, context.PublishedDirectories));

                    occupied = true;
                    continue;
                }

                child.Delete();
                continue;
            }

            if (context.Published.Contains(entryPath))
            {
                occupied = true;
                continue;
            }

            entry.Delete();
            context.Removed.Add(entryPath);
        }

        return occupied;
    }

    // A dot entry keeps a directory from being pruned, so after a rename in case alone, such as Blog to blog, it keeps
    // its old name, and a file system that ignores case writes the new pages into it. Renaming it fixes the spelling.
    // Returns the spelling the directory has afterwards.
    private string AdoptPublishedSpelling(
        IDirectoryInfo parent,
        IDirectoryInfo child,
        string relativePath,
        IFileSystemInfo[] siblings,
        HashSet<string> publishedDirectories)
    {
        IPath path = this._fileSystem.Path;

        // Every directory a build keeps for its pages is published under its own spelling, so only a leftover goes on.
        if (!publishedDirectories.TryGetValue(relativePath, out string? published) || published.Equals(relativePath, StringComparison.Ordinal)) return relativePath;

        string name = path.GetFileName(published);
        string target = path.Combine(parent.FullName, name);

        // A file system that respects case lists the published spelling as an entry of its own or cannot resolve it,
        // while one that ignores case resolves it to this very directory.
        if (siblings.Any(sibling => sibling.Name.Equals(name, StringComparison.Ordinal)) || !this._fileSystem.Directory.Exists(target)) return relativePath;

        // Through an unused name, because Directory.Move refuses a destination that already exists, and the new
        // spelling still resolves to this directory.
        string transit = path.Combine(parent.FullName, path.GetRandomFileName());

        this._fileSystem.Directory.Move(child.FullName, transit);
        this._fileSystem.Directory.Move(transit, target);

        return published;
    }

    #endregion
}
