using System;
using System.Collections.Generic;
using System.IO;

using Gelyn.Model.Options;
using Gelyn.Services;
using Gelyn.Tests.Stubs;

namespace Gelyn.Tests.Fixtures;

/// <summary>
///     A throwaway content tree under the temporary directory, together with a <see cref="SiteBuilder"/>
///     wired to generate it.
/// </summary>
/// <remarks>
///     The tree nests deeper than the navigation reaches, so that a test can tell the pages that are
///     written apart from the pages that are linked:
///     <code>
///     content/index.md             -> index.html              linked
///     content/about.md             -> about.html              linked
///     content/blog/index.md        -> blog/index.html         linked
///     content/blog/hello.md        -> blog/hello.html
///     content/blog/2026/index.md   -> blog/2026/index.html
///     content/blog/2026/q3/deep.md -> blog/2026/q3/deep.html
///     </code>
/// </remarks>
internal sealed class ContentFixture : IDisposable
{
    private readonly string _root;

    private ContentFixture(string root, SiteBuilder builder)
    {
        this._root = root;
        this.Builder = builder;
    }

    /// <summary>
    ///     Generates the fixture's content into the fixture's output directory.
    /// </summary>
    public SiteBuilder Builder { get; }

    /// <summary>
    ///     Writes the content tree and wires a builder to it.
    /// </summary>
    /// <returns>
    ///     A fixture whose directory is removed once it is disposed.
    /// </returns>
    public static ContentFixture Create()
    {
        string root = Path.Combine(Path.GetTempPath(), $"gelyn-{Guid.NewGuid():N}");

        Write(root, "content/index.md", "Home");
        Write(root, "content/about.md", "About");
        Write(root, "content/blog/index.md", "Blog");
        Write(root, "content/blog/hello.md", "Hello");
        Write(root, "content/blog/2026/index.md", "2026");
        Write(root, "content/blog/2026/q3/deep.md", "Deep");

        HostEnvironmentStub environment = new() { ContentRootPath = root };
        ContentWalker walker = new(new MarkdigRenderer(), environment);

        return new ContentFixture(root, new SiteBuilder(walker, environment, new OptionsMonitorStub()));
    }

    /// <summary>
    ///     Enumerates every page the builder has written.
    /// </summary>
    /// <returns>
    ///     The absolute path of each generated HTML file.
    /// </returns>
    public IEnumerable<string> EnumerateOutput()
    {
        string output = Path.Combine(this._root, new SiteOptions().OutputDirectory);

        return Directory.EnumerateFiles(output, "*.html", SearchOption.AllDirectories);
    }

    /// <summary>
    ///     Deletes the content tree together with everything generated into it.
    /// </summary>
    public void Dispose() => Directory.Delete(this._root, recursive: true);

    #region Helpers

    private static void Write(string root, string relativePath, string title)
    {
        string path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? root);
        File.WriteAllText(path, $"---{Environment.NewLine}title: {title}{Environment.NewLine}---");
    }

    #endregion
}
