using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.IO.Enumeration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Gelyn.Core;
using Gelyn.Core.Internals;
using Gelyn.Model;
using Gelyn.Model.Options;

using Microsoft.Extensions.Hosting;

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
///             <description><see cref="RenderAsync"/> reads and renders what the scan located.</description>
///         </item>
///     </list>
/// </remarks>
internal sealed class ContentPipeline
{
    private const string IndexFileName = "index.md";
    private const string IndexOutputFileName = "index.html";
    private const string MarkdownExtension = ".md";
    private const string StylesheetExtension = ".css";
    private const string ScriptExtension = ".js";

    private readonly IHostEnvironment _environment;
    private readonly IFileSystem _fileSystem;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ContentPipeline"/> class.
    /// </summary>
    /// <param name="environment">
    ///     The host environment relative paths are resolved against.
    /// </param>
    /// <param name="fileSystem">
    ///     The file system to work against.
    /// </param>
    public ContentPipeline(IHostEnvironment environment, IFileSystem fileSystem)
    {
        this._environment = environment;
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
    /// <exception cref="FileNotFoundException">
    ///     The content directory declares no <c>index.md</c>.
    /// </exception>
    public IReadOnlyList<ContentItem> Scan(SiteOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        IPath path = this._fileSystem.Path;
        string root = path.Combine(this._environment.ContentRootPath, options.ContentDirectory);
        string home = path.Combine(root, IndexFileName);

        if (!this._fileSystem.File.Exists(home))
            throw new FileNotFoundException($"No landing page found at '{home}'.", home);

        string hrefPrefix = ResolveBaseUrlPath(options.BaseUrl);

        return this.CollectItems(
            this._fileSystem.DirectoryInfo.New(root),
            prefix: [],
            inheritedStylesheets: [],
            inheritedScripts: [],
            hrefPrefix,
            options.ExcludePatterns);
    }

    /// <summary>
    ///     Reads each located page and renders its Markdown.
    /// </summary>
    /// <param name="pages">
    ///     The <see cref="ContentKind.Page"/> items a scan located, in the order they are to be rendered.
    /// </param>
    /// <param name="cancellationToken">
    ///     Token used to cancel the operation.
    /// </param>
    /// <returns>
    ///     Every page, in the order it was given.
    /// </returns>
    public async Task<IReadOnlyList<ContentPage>> RenderAsync(
        IReadOnlyList<ContentItem> pages,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pages);

        var rendered = new List<ContentPage>(pages.Count);

        foreach (ContentItem item in pages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string markdown = await this._fileSystem.File
                .ReadAllTextAsync(item.SourcePath, cancellationToken)
                .ConfigureAwait(false);

            RenderedMarkdown document = MarkdownRenderer.Render(markdown);
            rendered.Add(item.ToContentPage(document));
        }

        return rendered;
    }

    #region Helpers

    /// <summary>
    ///     Locates every file in one directory and in the directories below it, classifies each one and
    ///     resolves the path and URL it is published under.
    /// </summary>
    /// <param name="directory">
    ///     The directory to walk.
    /// </param>
    /// <param name="prefix">
    ///     The output-relative segments leading to <paramref name="directory"/>, empty at the content root.
    /// </param>
    /// <param name="inheritedStylesheets">
    ///     The root-relative URLs of the stylesheets declared above <paramref name="directory"/>, outermost
    ///     first.
    /// </param>
    /// <param name="inheritedScripts">
    ///     The root-relative URLs of the scripts declared above <paramref name="directory"/>, outermost first.
    /// </param>
    /// <param name="hrefPrefix">
    ///     The path of the configured base URL, or an empty string when the site is served from the root of a
    ///     domain.
    /// </param>
    /// <param name="excluded">
    ///     The name patterns an entry is left out for.
    /// </param>
    /// <returns>
    ///     Every file at and below <paramref name="directory"/>, landing page first, with the files the build
    ///     has no use for left out.
    /// </returns>
    private List<ContentItem> CollectItems(
        IDirectoryInfo directory,
        string[] prefix,
        IReadOnlyList<string> inheritedStylesheets,
        IReadOnlyList<string> inheritedScripts,
        string hrefPrefix,
        IReadOnlyList<string> excluded)
    {
        IFileSystemInfo[] entries = [.. directory
            .EnumerateFileSystemInfos()
            .Where(entry => !IsDenied(entry, excluded))
            .OrderBy(static entry => entry.Name, StringComparer.Ordinal)];

        IEnumerable<string> Linked(ContentKind kind) => entries
            .OfType<IFileInfo>()
            .Where(file => Classify(file) == kind)
            .Select(file => CombineHref(hrefPrefix, [.. prefix, file.Name]));

        // Resolved ahead of the items, because a page links what sits beside it as well as what sits above
        // it, and shared by reference with every page at this level rather than copied per page.
        string[] stylesheets = [.. inheritedStylesheets, .. Linked(ContentKind.Stylesheet)];
        string[] scripts = [.. inheritedScripts, .. Linked(ContentKind.Script)];

        // Files before subdirectories, index first among files, which is what makes the root index the
        // first result without the walk having to seed it ahead of itself.
        IEnumerable<IFileInfo> files = entries
            .OfType<IFileInfo>()
            .OrderByDescending(static file => file.Name.Equals(IndexFileName, StringComparison.Ordinal));

        List<ContentItem> items = [];

        foreach (IFileInfo file in files)
        {
            ContentKind kind = Classify(file);
            bool isPage = kind is ContentKind.Page;
            bool isIndex = file.Name.Equals(IndexFileName, StringComparison.Ordinal);
            string slug = this._fileSystem.Path.GetFileNameWithoutExtension(file.Name);

            // A copy keeps its own name, so only a rendered page is renamed to the extension it is served as.
            string name = isPage
                ? isIndex ? IndexOutputFileName : $"{slug}.html"
                : file.Name;

            string[] segments = [.. prefix, name];

            items.Add(new ContentItem
            {
                SourcePath = file.FullName,
                OutputPath = this._fileSystem.Path.Combine(segments),
                Href = CombineHref(hrefPrefix, segments),
                Kind = kind,
                FallbackTitle = isIndex && prefix.Length > 0 ? prefix[^1] : slug,
                InNavigation = isPage && IsInNavigation(segments),
                Stylesheets = isPage ? stylesheets : [],
                Scripts = isPage ? scripts : [],
            });
        }

        foreach (IDirectoryInfo child in entries.OfType<IDirectoryInfo>())
        {
            items.AddRange(this.CollectItems(
                child,
                [.. prefix, child.Name],
                inheritedStylesheets: stylesheets,
                inheritedScripts: scripts,
                hrefPrefix,
                excluded));
        }

        return items;
    }

    // The symlink rule is not configurable: a link lets the walk leave the content directory, one resolving
    // to an ancestor makes the walk unbounded, and File.Copy would publish the target's bytes, not the link.
    private static bool IsDenied(IFileSystemInfo entry, IReadOnlyList<string> excluded) =>
        entry.LinkTarget is not null
        || excluded.Any(pattern => FileSystemName.MatchesSimpleExpression(pattern, entry.Name));

    private static ContentKind Classify(IFileInfo file) => file.Extension switch
    {
        var extension when extension.Equals(MarkdownExtension, StringComparison.OrdinalIgnoreCase) => ContentKind.Page,
        var extension when extension.Equals(StylesheetExtension, StringComparison.OrdinalIgnoreCase) => ContentKind.Stylesheet,
        var extension when extension.Equals(ScriptExtension, StringComparison.OrdinalIgnoreCase) => ContentKind.Script,
        _ => ContentKind.MiscFile,
    };

    private static string CombineHref(string prefix, string[] segments) =>
        $"{prefix}/{string.Join('/', segments)}";

    private static string ResolveBaseUrlPath(Uri? baseUrl) =>
        baseUrl?.AbsolutePath.TrimEnd('/') ?? string.Empty;

    private static bool IsInNavigation(string[] segments) => segments.Length switch
    {
        1 => true,
        2 => segments[^1].Equals(IndexOutputFileName, StringComparison.Ordinal),
        _ => false,
    };

    #endregion
}
