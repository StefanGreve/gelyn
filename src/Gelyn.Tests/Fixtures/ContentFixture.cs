using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;

using Gelyn.Model.Options;
using Gelyn.Services;
using Gelyn.Tests.Stubs;

namespace Gelyn.Tests.Fixtures;

/// <summary>
///     An in-memory content tree, together with the walker and the builder that read it.
/// </summary>
/// <remarks>
///     Nothing touches the real file system, so there is no temporary directory to clean up and no test
///     that can be affected by what another test leaves behind.
/// </remarks>
internal sealed class ContentFixture
{
    private readonly string _root;

    private ContentFixture(MockFileSystem fileSystem, string root, SiteOptions options)
    {
        this._root = root;
        this.FileSystem = fileSystem;
        this.Options = options;
        this.ContentRoot = fileSystem.Path.Combine(root, options.ContentDirectory);

        HostEnvironmentStub environment = new() { ContentRootPath = root };

        this.Walker = new ContentWalker(new MarkdigRenderer(), environment, fileSystem);
        this.Builder = new SiteBuilder(this.Walker, environment, new OptionsMonitorStub(options), fileSystem);
    }

    /// <summary>
    ///     The file system both the walker and the builder are wired to.
    /// </summary>
    public MockFileSystem FileSystem { get; }

    /// <summary>
    ///     The directory the content tree is written to.
    /// </summary>
    public string ContentRoot { get; }

    /// <summary>
    ///     The options both the walker and the builder are driven by.
    /// </summary>
    public SiteOptions Options { get; }

    /// <summary>
    ///     Discovers and renders the fixture's content.
    /// </summary>
    public ContentWalker Walker { get; }

    /// <summary>
    ///     Generates the fixture's content into the fixture's output directory.
    /// </summary>
    public SiteBuilder Builder { get; }

    /// <summary>
    ///     Creates a fixture holding a tree that nests deeper than the navigation reaches, so that a test
    ///     can tell the pages that are written apart from the pages that are linked.
    /// </summary>
    /// <remarks>
    ///     <code>
    ///     index.md             -> index.html              linked
    ///     about.md             -> about.html              linked
    ///     blog/index.md        -> blog/index.html         linked
    ///     blog/hello.md        -> blog/hello.html
    ///     blog/2026/index.md   -> blog/2026/index.html
    ///     blog/2026/q3/deep.md -> blog/2026/q3/deep.html
    ///     </code>
    /// </remarks>
    /// <returns>
    ///     The new fixture.
    /// </returns>
    public static ContentFixture Create() =>
        CreateEmpty()
            .Write("index.md", "Home")
            .Write("about.md", "About")
            .Write("blog/index.md", "Blog")
            .Write("blog/hello.md", "Hello")
            .Write("blog/2026/index.md", "2026")
            .Write("blog/2026/q3/deep.md", "Deep");

    /// <summary>
    ///     Creates a fixture holding an empty content directory, for a test that supplies its own tree.
    /// </summary>
    /// <returns>
    ///     The new fixture.
    /// </returns>
    public static ContentFixture CreateEmpty()
    {
        MockFileSystem fileSystem = new();
        SiteOptions options = new();
        string root = fileSystem.Path.Combine(fileSystem.Path.GetTempPath(), "gelyn");

        ContentFixture fixture = new(fileSystem, root, options);

        fileSystem.AddDirectory(fixture.ContentRoot);

        return fixture;
    }

    /// <summary>
    ///     Adds one source file, creating the directories leading to it.
    /// </summary>
    /// <param name="relativePath">
    ///     The path of the file relative to <see cref="ContentRoot"/>, separated by forward slashes.
    /// </param>
    /// <param name="title">
    ///     The title to declare in the front matter, or <see langword="null"/> to write no front matter.
    /// </param>
    /// <returns>
    ///     The same fixture, so that calls can be chained.
    /// </returns>
    public ContentFixture Write(string relativePath, string? title = null)
    {
        this.FileSystem.AddFile(this.Resolve(relativePath), new MockFileData(title is null
            ? "# Untitled"
            : $"---{Environment.NewLine}title: {title}{Environment.NewLine}---"));

        return this;
    }

    /// <summary>
    ///     Reads a page the builder has written.
    /// </summary>
    /// <param name="path">
    ///     The absolute path of the generated file.
    /// </param>
    /// <returns>
    ///     The contents of the file.
    /// </returns>
    public string ReadOutput(string path) => this.FileSystem.File.ReadAllText(path);

    /// <summary>
    ///     Enumerates every page the builder has written.
    /// </summary>
    /// <returns>
    ///     The absolute path of each generated HTML file.
    /// </returns>
    public IEnumerable<string> EnumerateOutput()
    {
        string output = this.FileSystem.Path.Combine(this._root, this.Options.OutputDirectory);

        return this.FileSystem.Directory.EnumerateFiles(output, "*.html", SearchOption.AllDirectories);
    }

    #region Helpers

    private string Resolve(string relativePath) => this.FileSystem.Path.Combine(
        this.ContentRoot,
        relativePath.Replace('/', this.FileSystem.Path.DirectorySeparatorChar));

    #endregion
}
