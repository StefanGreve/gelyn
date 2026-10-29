using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;

using Gelyn.Model.Options;
using Gelyn.Services;
using Gelyn.Tests.Stubs;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Gelyn.Tests.Fixtures;

/// <summary>
///     An in-memory content tree, together with the pipeline and the builder that read it.
/// </summary>
/// <remarks>
///     Nothing touches the real file system, so there is no temporary directory to clean up and no test
///     that can be affected by what another test leaves behind.
/// </remarks>
internal sealed class ContentFixture
{
    private ContentFixture(MockFileSystem fileSystem, string root, SiteOptions options, LogLevel verbosity)
    {
        this.FileSystem = fileSystem;
        this.Options = options;
        this.Root = root;
        this.ContentRoot = fileSystem.Path.Combine(root, options.ContentDirectory);
        this.OutputRoot = fileSystem.Path.Combine(root, options.OutputDirectory);
        this.Logger = new FakeLogger<SiteBuilder>();

        // Every level is enabled by default, so the requested minimum has to be applied one level at a time.
        foreach (LogLevel level in Enum.GetValues<LogLevel>())
            this.Logger.ControlLevel(level, level != LogLevel.None && level >= verbosity);

        this.HostEnvironment = new HostEnvironmentStub { ContentRootPath = root };

        this.Pipeline = new ContentPipeline(this.HostEnvironment, fileSystem);

        this.Builder = new SiteBuilder(
            this.Pipeline,
            this.HostEnvironment,
            new OptionsMonitorStub(options),
            fileSystem,
            this.Logger);
    }

    /// <summary>
    ///     The file system both the pipeline and the builder are wired to.
    /// </summary>
    public MockFileSystem FileSystem { get; }

    /// <summary>
    ///     The directory holding both the content tree and the generated site.
    /// </summary>
    public string Root { get; }

    /// <summary>
    ///     The directory the content tree is written to.
    /// </summary>
    public string ContentRoot { get; }

    /// <summary>
    ///     The directory the generated site is written to.
    /// </summary>
    public string OutputRoot { get; }

    /// <summary>
    ///     The options both the pipeline and the builder are driven by.
    /// </summary>
    public SiteOptions Options { get; }

    /// <summary>
    ///     The host environment every relative path is resolved against, whose content root is
    ///     <see cref="Root"/>.
    /// </summary>
    public HostEnvironmentStub HostEnvironment { get; }

    /// <summary>
    ///     Discovers and renders the fixture's content.
    /// </summary>
    public ContentPipeline Pipeline { get; }

    /// <summary>
    ///     Generates the fixture's content into the fixture's output directory.
    /// </summary>
    public SiteBuilder Builder { get; }

    /// <summary>
    ///     The logger <see cref="Builder"/> reports its counters to.
    /// </summary>
    public FakeLogger<SiteBuilder> Logger { get; }

    /// <summary>
    ///     The records <see cref="Builder"/> has written, in the order it wrote them.
    /// </summary>
    public IReadOnlyList<FakeLogRecord> Records => this.Logger.Collector.GetSnapshot();

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
    /// <param name="verbosity">
    ///     The lowest level <see cref="Logger"/> reports as enabled.
    /// </param>
    /// <returns>
    ///     The new fixture.
    /// </returns>
    public static ContentFixture Create(LogLevel verbosity = LogLevel.Debug) =>
        CreateEmpty(verbosity)
            .Write("index.md", "Home")
            .Write("about.md", "About")
            .Write("blog/index.md", "Blog")
            .Write("blog/hello.md", "Hello")
            .Write("blog/2026/index.md", "2026")
            .Write("blog/2026/q3/deep.md", "Deep");

    /// <summary>
    ///     Creates a fixture holding a stylesheet at each of three levels, so that a test can tell a
    ///     stylesheet a page inherits apart from the one declared beside it.
    /// </summary>
    /// <remarks>
    ///     <code>
    ///     styles.css
    ///     index.md             -> index.html             styles.css
    ///     about.md             -> about.html             styles.css
    ///     blog/blog.css
    ///     blog/index.md        -> blog/index.html        styles.css + blog.css
    ///     blog/hello.md        -> blog/hello.html        styles.css + blog.css
    ///     blog/2026/year.css
    ///     blog/2026/index.md   -> blog/2026/index.html   styles.css + blog.css + year.css
    ///     </code>
    /// </remarks>
    /// <param name="verbosity">
    ///     The lowest level <see cref="Logger"/> reports as enabled.
    /// </param>
    /// <returns>
    ///     The new fixture.
    /// </returns>
    public static ContentFixture CreateStyled(LogLevel verbosity = LogLevel.Debug) =>
        CreateEmpty(verbosity)
            .WriteFile("styles.css", "body{margin:0}")
            .Write("index.md", "Home")
            .Write("about.md", "About")
            .WriteFile("blog/blog.css", "article{max-width:70ch}")
            .Write("blog/index.md", "Blog")
            .Write("blog/hello.md", "Hello")
            .WriteFile("blog/2026/year.css", "h1{color:teal}")
            .Write("blog/2026/index.md", "2026");

    /// <summary>
    ///     Creates a fixture holding a script at each of three levels, so that a test can tell a script a
    ///     page inherits apart from the one dropped beside it.
    /// </summary>
    /// <remarks>
    ///     <code>
    ///     app.js
    ///     index.md             -> index.html             app.js
    ///     about.md             -> about.html             app.js
    ///     blog/blog.js
    ///     blog/index.md        -> blog/index.html        app.js + blog.js
    ///     blog/hello.md        -> blog/hello.html        app.js + blog.js
    ///     blog/2026/year.js
    ///     blog/2026/index.md   -> blog/2026/index.html   app.js + blog.js + year.js
    ///     </code>
    /// </remarks>
    /// <param name="verbosity">
    ///     The lowest level <see cref="Logger"/> reports as enabled.
    /// </param>
    /// <returns>
    ///     The new fixture.
    /// </returns>
    public static ContentFixture CreateScripted(LogLevel verbosity = LogLevel.Debug) =>
        CreateEmpty(verbosity)
            .WriteFile("app.js", "globalThis.app = 1")
            .Write("index.md", "Home")
            .Write("about.md", "About")
            .WriteFile("blog/blog.js", "globalThis.blog = 1")
            .Write("blog/index.md", "Blog")
            .Write("blog/hello.md", "Hello")
            .WriteFile("blog/2026/year.js", "globalThis.year = 2026")
            .Write("blog/2026/index.md", "2026");

    /// <summary>
    ///     Creates a fixture holding one page that enables math rendering, beside a page and above a page
    ///     that do not, so that a test can confirm the flag reaches neither.
    /// </summary>
    /// <remarks>
    ///     <code>
    ///     index.md             -> index.html             no math
    ///     blog/index.md        -> blog/index.html        math
    ///     blog/hello.md        -> blog/hello.html        no math
    ///     blog/2026/index.md   -> blog/2026/index.html   no math
    ///     </code>
    /// </remarks>
    /// <param name="verbosity">
    ///     The lowest level <see cref="Logger"/> reports as enabled.
    /// </param>
    /// <returns>
    ///     The new fixture.
    /// </returns>
    public static ContentFixture CreateMathematical(LogLevel verbosity = LogLevel.Debug) =>
        CreateRendered("enableMathRendering", verbosity);

    /// <summary>
    ///     Creates a fixture holding one page that enables diagram rendering, beside a page and above a page
    ///     that do not, so that a test can confirm the flag reaches neither.
    /// </summary>
    /// <remarks>
    ///     <code>
    ///     index.md             -> index.html             no diagrams
    ///     blog/index.md        -> blog/index.html        diagrams
    ///     blog/hello.md        -> blog/hello.html        no diagrams
    ///     blog/2026/index.md   -> blog/2026/index.html   no diagrams
    ///     </code>
    /// </remarks>
    /// <param name="verbosity">
    ///     The lowest level <see cref="Logger"/> reports as enabled.
    /// </param>
    /// <returns>
    ///     The new fixture.
    /// </returns>
    public static ContentFixture CreateDiagrammatic(LogLevel verbosity = LogLevel.Debug) =>
        CreateRendered("enableDiagramRendering", verbosity);

    /// <summary>
    ///     Creates a fixture holding two pages that declare the same date, one that declares a different date
    ///     and one that declares none, so that a test can tell the footers apart by the date they carry.
    /// </summary>
    /// <remarks>
    ///     <code>
    ///     index.md             -> index.html              2026-02-01
    ///     about.md             -> about.html              2026-02-01
    ///     blog/index.md        -> blog/index.html         2026-03-15
    ///     blog/hello.md        -> blog/hello.html         no date
    ///     </code>
    /// </remarks>
    /// <param name="verbosity">
    ///     The lowest level <see cref="Logger"/> reports as enabled.
    /// </param>
    /// <returns>
    ///     The new fixture.
    /// </returns>
    public static ContentFixture CreateDated(LogLevel verbosity = LogLevel.Debug) =>
        CreateEmpty(verbosity)
            .WriteDated("index.md", "Home", "2026-02-01")
            .WriteDated("about.md", "About", "2026-02-01")
            .WriteDated("blog/index.md", "Blog", "2026-03-15")
            .Write("blog/hello.md", "Hello");

    /// <summary>
    ///     Creates a fixture holding a section index that carries a <c>[TOD]</c> marker, together with the
    ///     pages the resulting table is expected to list and the pages it is expected to leave out.
    /// </summary>
    /// <remarks>
    ///     <code>
    ///     index.md             -> index.html              2026-01-01
    ///     about.md             -> about.html              2026-01-15
    ///     blog/index.md        -> blog/index.html         2026-02-01   carries [TOD]
    ///     blog/hello.md        -> blog/hello.html         2026-03-01   listed
    ///     blog/draft.md        -> blog/draft.html         no date      a draft, so no table lists it
    ///     blog/2026/index.md   -> blog/2026/index.html    2026-06-01   listed, and marked as a folder
    ///     blog/2026/retro.md   -> blog/2026/retro.html    2026-07-01   a directory down, so not listed
    ///     blogs/other.md       -> blogs/other.html        2026-04-01   beside blog, so out of scope
    ///     </code>
    /// </remarks>
    /// <param name="verbosity">
    ///     The lowest level <see cref="Logger"/> reports as enabled.
    /// </param>
    /// <returns>
    ///     The new fixture.
    /// </returns>
    public static ContentFixture CreateTabulated(LogLevel verbosity = LogLevel.Debug) =>
        CreateEmpty(verbosity)
            .WriteDated("index.md", "Home", "2026-01-01")
            .WriteDated("about.md", "About", "2026-01-15")
            .WriteDated("blog/index.md", "Blog", "2026-02-01", "[TOD]")
            .WriteDated("blog/hello.md", "Hello", "2026-03-01")
            .Write("blog/draft.md", "Draft")
            .WriteDated("blog/2026/index.md", "Year 2026", "2026-06-01")
            .WriteDated("blog/2026/retro.md", "Retro", "2026-07-01")
            .WriteDated("blogs/other.md", "Other", "2026-04-01");

    /// <summary>
    ///     Creates a fixture holding an empty content directory, for a test that supplies its own tree.
    /// </summary>
    /// <param name="verbosity">
    ///     The lowest level <see cref="Logger"/> reports as enabled.
    /// </param>
    /// <returns>
    ///     The new fixture.
    /// </returns>
    public static ContentFixture CreateEmpty(LogLevel verbosity = LogLevel.Debug)
    {
        MockFileSystem fileSystem = new();
        SiteOptions options = new();
        string root = fileSystem.Path.Combine(fileSystem.Path.GetTempPath(), "gelyn");

        ContentFixture fixture = new(fileSystem, root, options, verbosity);

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
    public ContentFixture Write(string relativePath, string? title = null) =>
        this.WriteFile(relativePath, title is null ? "# Untitled" : $"---{Environment.NewLine}title: {title}{Environment.NewLine}---");

    /// <summary>
    ///     Adds one source file that declares a date, creating the directories leading to it.
    /// </summary>
    /// <param name="relativePath">
    ///     The path of the file relative to <see cref="ContentRoot"/>, separated by forward slashes.
    /// </param>
    /// <param name="title">
    ///     The title to declare in the front matter.
    /// </param>
    /// <param name="date">
    ///     The date to declare in the front matter, in ISO 8601 form.
    /// </param>
    /// <param name="body">
    ///     The Markdown to write below the front matter, which defaults to nothing.
    /// </param>
    /// <returns>
    ///     The same fixture, so that calls can be chained.
    /// </returns>
    public ContentFixture WriteDated(string relativePath, string title, string date, string body = "") =>
        this.WriteFile(
            relativePath,
            string.Join(Environment.NewLine, "---", $"title: {title}", $"date: {date}", "---", string.Empty, body));

    /// <summary>
    ///     Adds one file verbatim, creating the directories leading to it.
    /// </summary>
    /// <param name="relativePath">
    ///     The path of the file relative to <see cref="ContentRoot"/>, separated by forward slashes.
    /// </param>
    /// <param name="contents">
    ///     The exact text to write.
    /// </param>
    /// <returns>
    ///     The same fixture, so that calls can be chained.
    /// </returns>
    public ContentFixture WriteFile(string relativePath, string contents = "")
    {
        this.FileSystem.AddFile(this.Resolve(relativePath), new MockFileData(contents));

        return this;
    }

    /// <summary>
    ///     Adds one binary file, creating the directories leading to it.
    /// </summary>
    /// <param name="relativePath">
    ///     The path of the file relative to <see cref="ContentRoot"/>, separated by forward slashes.
    /// </param>
    /// <param name="contents">
    ///     The exact bytes to write.
    /// </param>
    /// <returns>
    ///     The same fixture, so that calls can be chained.
    /// </returns>
    public ContentFixture WriteBytes(string relativePath, byte[] contents)
    {
        this.FileSystem.AddFile(this.Resolve(relativePath), new MockFileData(contents));

        return this;
    }

    /// <summary>
    ///     Adds one file outside the content tree, where nothing the build locates may reach it.
    /// </summary>
    /// <param name="relativePath">
    ///     The path of the file relative to <see cref="Root"/>, separated by forward slashes.
    /// </param>
    /// <param name="contents">
    ///     The exact text to write.
    /// </param>
    /// <returns>
    ///     The same fixture, so that calls can be chained.
    /// </returns>
    public ContentFixture WriteOutside(string relativePath, string contents = "")
    {
        this.FileSystem.AddFile(this.Resolve(this.Root, relativePath), new MockFileData(contents));

        return this;
    }

    /// <summary>
    ///     Links one entry of the content tree to a file outside it, as a convenience link in a working copy
    ///     would.
    /// </summary>
    /// <param name="relativePath">
    ///     The path the link is created at, relative to <see cref="ContentRoot"/>.
    /// </param>
    /// <param name="targetPath">
    ///     The path of the file the link resolves to, relative to <see cref="Root"/>.
    /// </param>
    /// <returns>
    ///     The same fixture, so that calls can be chained.
    /// </returns>
    public ContentFixture LinkFile(string relativePath, string targetPath)
    {
        this.FileSystem.File.CreateSymbolicLink(
            this.Resolve(relativePath),
            this.Resolve(this.Root, targetPath));

        return this;
    }

    /// <summary>
    ///     Removes one source file, as renaming or deleting it between two builds would.
    /// </summary>
    /// <param name="relativePath">
    ///     The path of the file relative to <see cref="ContentRoot"/>, separated by forward slashes.
    /// </param>
    /// <returns>
    ///     The same fixture, so that calls can be chained.
    /// </returns>
    public ContentFixture Delete(string relativePath)
    {
        this.FileSystem.File.Delete(this.Resolve(this.ContentRoot, relativePath));

        return this;
    }

    /// <summary>
    ///     Adds one file to the output directory, as a deploy target or an earlier build would have left it
    ///     there, creating the directories leading to it.
    /// </summary>
    /// <param name="relativePath">
    ///     The path of the file relative to <see cref="OutputRoot"/>, separated by forward slashes.
    /// </param>
    /// <param name="contents">
    ///     The exact text to write.
    /// </param>
    /// <returns>
    ///     The same fixture, so that calls can be chained.
    /// </returns>
    public ContentFixture WriteOutput(string relativePath, string contents = "")
    {
        this.FileSystem.AddFile(this.Resolve(this.OutputRoot, relativePath), new MockFileData(contents));

        return this;
    }

    /// <summary>
    ///     Indicates whether the output directory holds a file at the given path.
    /// </summary>
    /// <param name="relativePath">
    ///     The path of the file relative to <see cref="OutputRoot"/>, separated by forward slashes.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> when the file is present, <see langword="false"/> otherwise.
    /// </returns>
    public bool OutputHolds(string relativePath) =>
        this.FileSystem.File.Exists(this.Resolve(this.OutputRoot, relativePath));

    /// <summary>
    ///     Indicates whether the output directory holds a directory at the given path.
    /// </summary>
    /// <param name="relativePath">
    ///     The path of the directory relative to <see cref="OutputRoot"/>, separated by forward slashes.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> when the directory is present, <see langword="false"/> otherwise.
    /// </returns>
    public bool OutputHoldsDirectory(string relativePath) =>
        this.FileSystem.Directory.Exists(this.Resolve(this.OutputRoot, relativePath));

    /// <summary>
    ///     Applies a base URL to the fixture's options, so that a test can exercise the path prefix and the
    ///     canonical reference that derive from it.
    /// </summary>
    /// <param name="baseUrl">
    ///     The absolute URL the site is served from. Only values the validator would accept are useful here,
    ///     because every consumer trusts it to have run.
    /// </param>
    /// <returns>
    ///     The same fixture, so that calls can be chained.
    /// </returns>
    public ContentFixture WithBaseUrl(string baseUrl)
    {
        this.Options.BaseUrl = new Uri(baseUrl, UriKind.Absolute);

        return this;
    }

    /// <summary>
    ///     Applies exclude patterns to the fixture's options, as a configuration file or the
    ///     <c>--exclude</c> flag would.
    /// </summary>
    /// <param name="patterns">
    ///     The patterns to match against each file and directory name.
    /// </param>
    /// <returns>
    ///     The same fixture, so that calls can be chained.
    /// </returns>
    public ContentFixture WithExcludePatterns(params string[] patterns)
    {
        this.Options.ExcludePatterns = patterns;

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
    ///     Enumerates the files the builder has produced.
    /// </summary>
    /// <param name="pattern">
    ///     The search pattern to match against, which defaults to the generated pages alone.
    /// </param>
    /// <returns>
    ///     The absolute path of each matching file.
    /// </returns>
    public IEnumerable<string> EnumerateOutput(string pattern = "*.html") =>
        this.FileSystem.Directory.EnumerateFiles(this.OutputRoot, pattern, SearchOption.AllDirectories);

    #region Helpers

    // One tree for both renderer flags, because what each test asserts is which pages the key reaches.
    private static ContentFixture CreateRendered(string key, LogLevel verbosity)
    {
        string block = string.Join(Environment.NewLine, "---", "title: Blog", $"{key}: true", "---");

        return CreateEmpty(verbosity)
            .Write("index.md", "Home")
            .WriteFile("blog/index.md", block)
            .Write("blog/hello.md", "Hello")
            .Write("blog/2026/index.md", "2026");
    }

    private string Resolve(string relativePath) => this.Resolve(this.ContentRoot, relativePath);

    private string Resolve(string root, string relativePath) => this.FileSystem.Path.Combine(
        root,
        relativePath.Replace('/', this.FileSystem.Path.DirectorySeparatorChar));

    #endregion
}
