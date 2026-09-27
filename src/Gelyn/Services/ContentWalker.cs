using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Gelyn.Abstractions;
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
public sealed class ContentWalker
{
    private const string IndexFileName = "index.md";
    private const string IndexOutputFileName = "index.html";
    private const string MarkdownExtension = ".md";

    private readonly MarkdownRendererContract _renderer;
    private readonly IHostEnvironment _environment;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ContentWalker"/> class.
    /// </summary>
    /// <param name="renderer">
    ///     Converts each Markdown source to HTML.
    /// </param>
    /// <param name="environment">
    ///     Supplies the root that a relative content directory is resolved against.
    /// </param>
    public ContentWalker(MarkdownRendererContract renderer, IHostEnvironment environment)
    {
        this._renderer = renderer;
        this._environment = environment;
    }

    /// <summary>
    ///     Discovers and renders every page of the site.
    /// </summary>
    /// <param name="options">
    ///     Supplies the content directory.
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

        string root = Path.Combine(this._environment.ContentRootPath, options.ContentDirectory);
        string home = Path.Combine(root, IndexFileName);

        if (!File.Exists(home))
            throw new FileNotFoundException($"No landing page found at '{home}'.", home);

        var pages = new List<ContentPage>();

        foreach (ContentSource source in EnumerateSources(root))
        {
            cancellationToken.ThrowIfCancellationRequested();

            string markdown = await File
                .ReadAllTextAsync(source.SourcePath, cancellationToken)
                .ConfigureAwait(false);

            RenderedMarkdown rendered = this._renderer.Render(markdown);

            pages.Add(new ContentPage
            {
                OutputPath = Path.Combine(source.Segments),
                Href = $"/{string.Join('/', source.Segments)}",
                Title = rendered.FrontMatter.Title ?? source.FallbackTitle,
                Html = rendered.Html,
                InNavigation = source.InNavigation,
            });
        }

        return pages;
    }

    #region Helpers

    private static IEnumerable<ContentSource> EnumerateSources(string root)
    {
        // Seed the landing page before the walk
        yield return new ContentSource
        {
            SourcePath = Path.Combine(root, IndexFileName),
            Segments = [IndexOutputFileName],
            FallbackTitle = Path.GetFileNameWithoutExtension(IndexFileName),
            InNavigation = true,
        };

        foreach (ContentSource source in EnumerateDirectory(new DirectoryInfo(root), []))
            yield return source;
    }

    private static IEnumerable<ContentSource> EnumerateDirectory(DirectoryInfo directory, string[] prefix)
    {
        IEnumerable<FileSystemInfo> entries = directory
            .EnumerateFileSystemInfos()
            .OrderBy(static entry => entry.Name, StringComparer.Ordinal);

        foreach (FileSystemInfo entry in entries)
        {
            if (entry is DirectoryInfo child)
            {
                // Descending into a symlink would let the walk leave the content directory entirely,
                // and a link that resolves to an ancestor makes the walk unbounded.
                if (child.LinkTarget is null)
                {
                    foreach (ContentSource source in EnumerateDirectory(child, [.. prefix, child.Name]))
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

            string slug = Path.GetFileNameWithoutExtension(entry.Name);
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

    private sealed record ContentSource
    {
        public required string SourcePath { get; init; }

        public required string[] Segments { get; init; }

        public required string FallbackTitle { get; init; }

        public required bool InNavigation { get; init; }
    }

    #endregion
}
