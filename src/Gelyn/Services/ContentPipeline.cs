using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Gelyn.Core;
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
///             <description><see cref="Scan"/> locates the pages without reading their contents.</description>
///         </item>
///         <item>
///             <description><see cref="RenderAsync"/> reads and renders what the scan located.</description>
///         </item>
///     </list>
/// </remarks>
public sealed class ContentPipeline
{
    private const string IndexFileName = "index.md";
    private const string IndexOutputFileName = "index.html";
    private const string MarkdownExtension = ".md";

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
    ///     Locates every page of the site and resolves the paths and URLs it is published under.
    /// </summary>
    /// <param name="options">
    ///     Supplies the site configuration the scan is driven by.
    /// </param>
    /// <returns>
    ///     Every page, landing page first.
    /// </returns>
    /// <exception cref="FileNotFoundException">
    ///     The content directory declares no <c>index.md</c>.
    /// </exception>
    internal IReadOnlyList<ContentItem> Scan(SiteOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        IPath path = this._fileSystem.Path;
        string root = path.Combine(this._environment.ContentRootPath, options.ContentDirectory);
        string home = path.Combine(root, IndexFileName);

        if (!this._fileSystem.File.Exists(home))
            throw new FileNotFoundException($"No landing page found at '{home}'.", home);

        string hrefPrefix = options.BaseUrl is Uri baseUrl ? baseUrl.AbsolutePath.TrimEnd('/') : string.Empty;
        List<ContentItem> items = [];

        this.Collect(this._fileSystem.DirectoryInfo.New(root), [], hrefPrefix, items);

        return items;
    }

    /// <summary>
    ///     Reads each located page and renders its Markdown.
    /// </summary>
    /// <param name="pages">
    ///     The pages a scan located, in the order they are to be rendered.
    /// </param>
    /// <param name="options">
    ///     Supplies the site configuration the render is driven by.
    /// </param>
    /// <param name="cancellationToken">
    ///     Token used to cancel the operation.
    /// </param>
    /// <returns>
    ///     Every page, in the order it was given.
    /// </returns>
    internal async Task<IReadOnlyList<ContentPage>> RenderAsync(
        IReadOnlyList<ContentItem> pages,
        SiteOptions options,
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

            rendered.Add(new ContentPage
            {
                OutputPath = item.OutputPath,
                Href = item.Href,
                Title = document.FrontMatter?.Title ?? item.FallbackTitle,
                Description = document.FrontMatter?.Description,
                Date = document.FrontMatter?.Date,
                Html = document.Html,
                InNavigation = item.InNavigation,
            });
        }

        return rendered;
    }

    #region Helpers

    private void Collect(IDirectoryInfo directory, string[] prefix, string hrefPrefix, List<ContentItem> items)
    {
        IFileSystemInfo[] entries = [.. directory
            .EnumerateFileSystemInfos()
            .OrderBy(static entry => entry.Name, StringComparer.Ordinal)];

        // Files before subdirectories, index first among files, which is what makes the root index the
        // first result without the walk having to seed it ahead of itself.
        IEnumerable<IFileInfo> files = entries
            .OfType<IFileInfo>()
            .Where(static file => file.Extension.Equals(MarkdownExtension, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(static file => file.Name.Equals(IndexFileName, StringComparison.Ordinal));

        foreach (IFileInfo file in files)
        {
            bool isIndex = file.Name.Equals(IndexFileName, StringComparison.Ordinal);
            string slug = this._fileSystem.Path.GetFileNameWithoutExtension(file.Name);
            string[] segments = [.. prefix, isIndex ? IndexOutputFileName : $"{slug}.html"];

            items.Add(new ContentItem
            {
                SourcePath = file.FullName,
                OutputPath = this._fileSystem.Path.Combine(segments),
                Href = Href(hrefPrefix, segments),
                FallbackTitle = isIndex && prefix.Length > 0 ? prefix[^1] : slug,
                InNavigation = IsInNavigation(segments),
            });
        }

        foreach (IDirectoryInfo child in entries.OfType<IDirectoryInfo>())
        {
            // Descending into a symlink would let the walk leave the content directory entirely,
            // and a link that resolves to an ancestor makes the walk unbounded.
            if (child.LinkTarget is null)
                this.Collect(child, [.. prefix, child.Name], hrefPrefix, items);
        }
    }

    // Output-relative segments to a root-relative URL: always forward slashes, whatever the platform uses.
    private static string Href(string prefix, string[] segments) => $"{prefix}/{string.Join('/', segments)}";

    // The navigation stays flat by design: root-level pages and the index of a top-level section only.
    private static bool IsInNavigation(string[] segments) => segments.Length switch
    {
        1 => true,
        2 => segments[^1].Equals(IndexOutputFileName, StringComparison.Ordinal),
        _ => false,
    };

    #endregion
}
