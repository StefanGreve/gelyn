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
///     Walks the content directory and renders every page it finds.
/// </summary>
/// <remarks>
///     The landing page is always the first result, so that callers can rely on the order without sorting.
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
    ///     Discovers and renders every page of the site.
    /// </summary>
    /// <param name="options">
    ///     Supplies the site configuration the walk is driven by.
    /// </param>
    /// <param name="cancellationToken">
    ///     Token used to cancel the operation.
    /// </param>
    /// <returns>
    ///     Every page, landing page first.
    /// </returns>
    /// <exception cref="FileNotFoundException">
    ///     The content directory declares no <c>index.md</c>.
    /// </exception>
    public async Task<IReadOnlyList<ContentPage>> WalkAsync(SiteOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        IPath path = this._fileSystem.Path;
        string root = path.Combine(this._environment.ContentRootPath, options.ContentDirectory);
        string home = path.Combine(root, IndexFileName);

        if (!this._fileSystem.File.Exists(home))
            throw new FileNotFoundException($"No landing page found at '{home}'.", home);

        string prefix = options.BaseUrl is Uri baseUrl ? baseUrl.AbsolutePath.TrimEnd('/') : string.Empty;
        var pages = new List<ContentPage>();

        foreach (ContentSource source in this.EnumerateSources(root))
        {
            cancellationToken.ThrowIfCancellationRequested();

            string markdown = await this._fileSystem.File
                .ReadAllTextAsync(source.SourcePath, cancellationToken)
                .ConfigureAwait(false);

            RenderedMarkdown rendered = MarkdownRenderer.Render(markdown);

            pages.Add(new ContentPage
            {
                OutputPath = path.Combine(source.Segments),
                Href = $"{prefix}/{string.Join('/', source.Segments)}",
                Title = rendered.FrontMatter?.Title ?? source.FallbackTitle,
                Description = rendered.FrontMatter?.Description,
                Date = rendered.FrontMatter?.Date,
                Html = rendered.Html,
                InNavigation = source.InNavigation,
            });
        }

        return pages;
    }

    #region Helpers

    private IEnumerable<ContentSource> EnumerateSources(string root)
    {
        // Seed the landing page before the walk
        yield return new ContentSource
        {
            SourcePath = this._fileSystem.Path.Combine(root, IndexFileName),
            Segments = [IndexOutputFileName],
            FallbackTitle = this._fileSystem.Path.GetFileNameWithoutExtension(IndexFileName),
            InNavigation = true,
        };

        IDirectoryInfo directory = this._fileSystem.DirectoryInfo.New(root);

        foreach (ContentSource source in this.EnumerateDirectory(directory, []))
            yield return source;
    }

    private IEnumerable<ContentSource> EnumerateDirectory(IDirectoryInfo directory, string[] prefix)
    {
        IEnumerable<IFileSystemInfo> entries = directory
            .EnumerateFileSystemInfos()
            .OrderBy(static entry => entry.Name, StringComparer.Ordinal);

        foreach (IFileSystemInfo entry in entries)
        {
            if (entry is IDirectoryInfo child)
            {
                // Descending into a symlink would let the walk leave the content directory entirely,
                // and a link that resolves to an ancestor makes the walk unbounded.
                if (child.LinkTarget is null)
                {
                    foreach (ContentSource source in this.EnumerateDirectory(child, [.. prefix, child.Name]))
                        yield return source;
                }

                continue;
            }

            if (!entry.Extension.Equals(MarkdownExtension, StringComparison.OrdinalIgnoreCase))
                continue;

            bool isIndex = entry.Name.Equals(IndexFileName, StringComparison.Ordinal);

            // The root index.md is the landing page, which EnumerateSources has already yielded
            if (isIndex && prefix.Length == 0)
                continue;

            string slug = this._fileSystem.Path.GetFileNameWithoutExtension(entry.Name);
            string[] segments = [.. prefix, isIndex ? IndexOutputFileName : $"{slug}.html"];

            yield return new ContentSource
            {
                SourcePath = entry.FullName,
                Segments = segments,
                FallbackTitle = isIndex ? prefix[^1] : slug,
                InNavigation = IsInNavigation(segments),
            };
        }
    }

    // The navigation stays flat by design: root-level pages and the index of a top-level section only.
    private static bool IsInNavigation(string[] segments) => segments.Length switch
    {
        1 => true,
        2 => segments[^1].Equals(IndexOutputFileName, StringComparison.Ordinal),
        _ => false,
    };

    #endregion
}
