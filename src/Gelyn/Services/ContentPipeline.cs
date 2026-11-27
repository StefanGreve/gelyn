using System.IO.Abstractions;
using System.IO.Enumeration;

using Gelyn.Core;
using Gelyn.Core.Context;
using Gelyn.Core.Internals;
using Gelyn.Exceptions;
using Gelyn.Extensions;
using Gelyn.Model;
using Gelyn.Model.Options;

using SharpYaml;

namespace Gelyn.Services;

/// <summary>
///     Discovers the files the site is built from, and renders the Markdown among them.
/// </summary>
/// <remarks>
///     The content pipeline is processed in this order:
///     <list type="number">
///         <item>
///             <description><see cref="Scan"/> locates the files without reading their contents.</description>
///         </item>
///         <item>
///             <description><see cref="Render"/> reads and renders what the scan located.</description>
///         </item>
///     </list>
/// </remarks>
internal sealed class ContentPipeline
{
    private const string IndexFileName = "index.md";
    private const string IndexSlug = "index";
    private const string LandingPageOutputPath = $"{IndexSlug}{FileExtensions.Html}";
    private const string NotFoundSlug = "404";

    private readonly string _contentRoot;
    private readonly IFileSystem _fileSystem;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ContentPipeline"/> class.
    /// </summary>
    /// <param name="contentRoot">
    ///     The directory relative paths are resolved against.
    /// </param>
    /// <param name="fileSystem">
    ///     The file system to work against.
    /// </param>
    public ContentPipeline(string contentRoot, IFileSystem fileSystem)
    {
        this._contentRoot = contentRoot;
        this._fileSystem = fileSystem;
    }

    /// <summary>
    ///     Locates every file the site is published from, classifies it and resolves the path and URL it is
    ///     published under.
    /// </summary>
    /// <param name="options">
    ///     Supplies the site configuration the scan is driven by.
    /// </param>
    /// <returns>
    ///     Every file, landing page first, with the files the build has no use for left out.
    /// </returns>
    /// <exception cref="ConfigurationException">
    ///     The scan publishes no <c>index.md</c> at the root of the content directory.
    /// </exception>
    /// <exception cref="BuildException">
    ///     Two entries publish one path.
    /// </exception>
    public IReadOnlyList<ContentItem> Scan(SiteOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        IPath path = this._fileSystem.Path;
        string root = path.Combine(this._contentRoot, options.ContentDirectory);
        IDirectoryInfo directory = this._fileSystem.DirectoryInfo.New(root);

        string hrefPrefix = UrlUtils.ResolveBasePath(options.BaseUrl);
        IReadOnlyList<ThemeFile> theme = options.Theme is string name ? ThemeCatalog.Files(name) : [];

        List<ContentItem> items = [];

        if (directory.Exists)
        {
            this.CollectItems(items, directory, new ScanContext
            {
                Prefix = [],
                Stylesheets = CollectThemeHrefs(theme, hrefPrefix, ContentKind.Stylesheet),
                Scripts = CollectThemeHrefs(theme, hrefPrefix, ContentKind.Script),
                Favicon = null,
                HrefPrefix = hrefPrefix,
                Excluded = options.ExcludePatterns,
            });
        }

        // Decided after the walk, because only a landing page the walk publishes counts, never a link or an excluded name.
        if (items is not [{ Kind: ContentKind.Page, OutputPath: LandingPageOutputPath }, ..])
        {
            string home = path.Combine(root, IndexFileName);
            throw new ConfigurationException($"No landing page found at '{home}'.");
        }

        return items;
    }

    /// <summary>
    ///     Reads each located page, renders its Markdown and resolves the files its front matter declares.
    /// </summary>
    /// <remarks>
    ///     Pages are rendered several at a time. When more than one fails, the failure of the first page in
    ///     order surfaces, as it would if they were rendered one after another.
    /// </remarks>
    /// <param name="items">
    ///     Every item a scan located, in the order the pages among them are to be rendered. The other items
    ///     are the files a page's front matter may declare.
    /// </param>
    /// <param name="bodies">
    ///     The owner the body of each page is allocated from.
    /// </param>
    /// <param name="cancellationToken">
    ///     Token used to cancel the operation.
    /// </param>
    /// <returns>
    ///     Every <see cref="ContentKind.Page"/>, in the order it was given.
    /// </returns>
    /// <exception cref="YamlException">
    ///     A front matter block is not well-formed YAML.
    /// </exception>
    /// <exception cref="BuildException">
    ///     A front matter block declares a stylesheet or a script the scan did not locate.
    /// </exception>
    public IReadOnlyList<ContentPage> Render(IReadOnlyList<ContentItem> items, PageBodies bodies, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(bodies);

        IPath path = this._fileSystem.Path;

        // Ordinal on every platform, so a declaration in the wrong case fails on Windows too and not only once served.
        var files = items
            .Where(static item => item.Kind is not ContentKind.Page)
            .ToDictionary(item => path.GetFullPath(item.SourcePath), StringComparer.Ordinal);

        ContentItem[] pages = [.. items.Where(static item => item.Kind is ContentKind.Page)];

        return OrderedParallel.Select(pages, item => this.RenderPage(item, files, bodies), cancellationToken);
    }

    #region Helpers

    // Read synchronously, because the caller already runs pages in parallel and an asynchronous read of a
    // small file costs more in overlapped I/O and continuations than it frees up.
    private ContentPage RenderPage(ContentItem item, Dictionary<string, ContentItem> files, PageBodies bodies)
    {
        string markdown = this._fileSystem.File.ReadText(item.SourcePath);

        RenderedMarkdown document = MarkdownRenderer.Render(markdown, bodies);
        FrontMatter? frontMatter = document.FrontMatter;

        IReadOnlyList<string> stylesheets = this.ResolveAssets(item, item.Stylesheets, frontMatter?.Stylesheets, ContentKind.Stylesheet, files);
        IReadOnlyList<string> scripts = this.ResolveAssets(item, item.Scripts, frontMatter?.Scripts, ContentKind.Script, files);

        return item.ToContentPage(document) with
        {
            Stylesheets = stylesheets,
            Scripts = scripts
        };
    }

    /// <summary>
    ///     Locates every file in one directory and in the directories below it, classifies each one and
    ///     resolves the path and URL it is published under.
    /// </summary>
    /// <param name="items">
    ///     The list every file at and below <paramref name="directory"/> is appended to, each directory's
    ///     index ahead of its other files, with the files the build has no use for left out.
    /// </param>
    /// <param name="directory">
    ///     The directory to walk.
    /// </param>
    /// <param name="context">
    ///     What this level inherits from the level above it.
    /// </param>
    private void CollectItems(List<ContentItem> items, IDirectoryInfo directory, ScanContext context)
    {
        IFileSystemInfo[] entries = [.. directory.ListEntries().Where(entry => !IsDenied(entry, context.Excluded))];

        // Files before subdirectories, index first among files, which is what makes the root index the
        // first result without the walk having to seed it ahead of itself.
        SourceFile[] files = [.. entries
            .OfType<IFileInfo>()
            .Select(this.ClassifyFile)
            .OrderByDescending(static file => file.IsIndex)];

        // Resolved ahead of the items, because a page links what sits beside it as well as what sits above
        // it, and shared by reference with every page at this level instead of copied per page.
        string[] stylesheets = AppendHrefs(context.Stylesheets, CollectHrefs(files, context, ContentKind.Stylesheet));
        string[] scripts = AppendHrefs(context.Scripts, CollectHrefs(files, context, ContentKind.Script));

        // The nearest declaration wins, because a document carries one icon and not a chain of them.
        string? favicon = CollectHrefs(files, context, ContentKind.Favicon).FirstOrDefault() ?? context.Favicon;

        // Every name this directory publishes, keyed without regard to case, so that a pair a file system ignoring
        // case would merge into one path is caught on every platform.
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (SourceFile file in files)
        {
            bool isPage = file.Kind is ContentKind.Page;
            bool isNotFound = isPage && context.Prefix.Count == 0 && file.Slug.Equals(NotFoundSlug, StringComparison.Ordinal);

            // A copy keeps its own name, so only a rendered page is renamed to the extension it is served as.
            string name = isPage ? $"{file.Slug}{FileExtensions.Html}" : file.Info.Name;
            string[] segments = [.. context.Prefix, name];
            string outputPath = this._fileSystem.Path.Combine(segments);

            if (!names.TryAdd(name, file.Info.FullName))
                throw new BuildException(DescribeCollision(names[name], file.Info.FullName, outputPath));

            items.Add(new ContentItem
            {
                SourcePath = file.Info.FullName,
                OutputPath = outputPath,
                Href = CombineHref(context.HrefPrefix, name),
                Kind = file.Kind,
                FallbackTitle = file.IsIndex && context.Prefix.Count > 0 ? context.Prefix[^1] : file.Stem,
                InNavigation = isPage && !isNotFound && IsInNavigation(file, context.Prefix.Count),
                IsNotFoundPage = isNotFound,
                Stylesheets = isPage ? stylesheets : [],
                Scripts = isPage ? scripts : [],
                Favicon = isPage ? favicon : null,
                SourceLastWriteTimeUtc = file.Info.LastWriteTimeUtc,
            });
        }

        foreach (IDirectoryInfo child in entries.OfType<IDirectoryInfo>())
        {
            int count = items.Count;

            this.CollectItems(items, child, context with
            {
                Prefix = [.. context.Prefix, child.Name],
                HrefPrefix = CombineHref(context.HrefPrefix, child.Name),
                Stylesheets = stylesheets,
                Scripts = scripts,
                Favicon = favicon,
            });

            // A directory that publishes nothing is never written, so its name is free.
            if (items.Count > count && !names.TryAdd(child.Name, child.FullName))
            {
                string outputPath = this._fileSystem.Path.Combine([.. context.Prefix, child.Name]);
                throw new BuildException(DescribeCollision(names[child.Name], child.FullName, outputPath));
            }
        }
    }

    private SourceFile ClassifyFile(IFileInfo file)
    {
        string stem = this._fileSystem.Path.GetFileNameWithoutExtension(file.Name);

        // A URL path is case-sensitive, so the slug is lowercase and a page's URL never varies with the case of its
        // file name: https://www.rfc-editor.org/rfc/rfc3986#section-6.2.2.1
        return new SourceFile(file, ContentKind.FromFileName(file.Name), stem, stem.ToLowerInvariant());
    }

    private IReadOnlyList<string> ResolveAssets(
        ContentItem page,
        IReadOnlyList<string> placed,
        IReadOnlyList<string?>? declared,
        ContentKind kind,
        Dictionary<string, ContentItem> files)
    {
        if (declared is null || declared.Count == 0) return placed;

        IPath path = this._fileSystem.Path;
        string directory = path.GetDirectoryName(page.SourcePath) ?? string.Empty;
        List<string> hrefs = [.. placed];

        foreach (string? value in declared)
        {
            string? source = string.IsNullOrWhiteSpace(value) ? null : path.GetFullPath(path.Combine(directory, value));

            if (source is null || !files.TryGetValue(source, out ContentItem? file) || file.Kind != kind)
            {
                string key = kind is ContentKind.Stylesheet ? "stylesheets" : "scripts";
                string noun = kind is ContentKind.Stylesheet ? "stylesheet" : "script";

                throw new BuildException($"'{value}' under {key} in '{page.SourcePath}' is not a {noun} in the content directory.");
            }

            if (!hrefs.Contains(file.Href, StringComparer.Ordinal))
                hrefs.Add(file.Href);
        }

        return hrefs;
    }

    // The symlink rule is not configurable: a link lets the walk leave the content directory, one resolving
    // to an ancestor makes the walk unbounded, and File.Copy would publish the target's bytes, not the link.
    private static bool IsDenied(IFileSystemInfo entry, IReadOnlyList<string> excluded) =>
        entry.IsLink || excluded.Any(pattern => FileSystemName.MatchesSimpleExpression(pattern, entry.Name));

    // The files of one kind directly in this directory, never below it, as the hrefs they are served at.
    private static IEnumerable<string> CollectHrefs(SourceFile[] files, ScanContext context, ContentKind kind) =>
        files.Where(file => file.Kind == kind).Select(file => CombineHref(context.HrefPrefix, file.Info.Name));

    // An href the level already inherits is left out. Only a root file replacing a file of the theme can be one,
    // so the replacement is linked once and in the theme's place.
    private static string[] AppendHrefs(IReadOnlyList<string> inherited, IEnumerable<string> placed) =>
        [.. inherited, .. placed.Where(href => !inherited.Contains(href, StringComparer.Ordinal))];

    private static string[] CollectThemeHrefs(IReadOnlyList<ThemeFile> theme, string prefix, ContentKind kind) =>
        [.. theme.Where(file => file.Kind == kind).Select(file => CombineHref(prefix, file.FileName))];

    // A name may hold '#', '?' or '%', which would end the path or start an escape if it were copied as is.
    private static string CombineHref(string prefix, string name) =>
        $"{prefix}/{Uri.EscapeDataString(name)}";

    private static string DescribeCollision(string first, string second, string outputPath) =>
        $"'{first}' and '{second}' both publish '{outputPath}'.";

    private static bool IsInNavigation(SourceFile file, int depth) =>
        depth switch
        {
            0 => true,
            1 => file.IsIndex,
            _ => false,
        };

    #endregion

    // A file of the walk, classified once, so that its kind and every role it plays agree.
    private sealed record SourceFile(IFileInfo Info, ContentKind Kind, string Stem, string Slug)
    {
        public bool IsIndex => this.Kind is ContentKind.Page && this.Slug.Equals(IndexSlug, StringComparison.Ordinal);
    }
}
