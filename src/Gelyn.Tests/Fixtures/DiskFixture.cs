using System;
using System.IO;
using System.IO.Abstractions;

using Gelyn.Model.Options;
using Gelyn.Services;
using Gelyn.Tests.Stubs;

using Microsoft.Extensions.Logging.Testing;

namespace Gelyn.Tests.Fixtures;

/// <summary>
///     A content tree on the real file system, together with the pipeline and the builder that read it.
/// </summary>
/// <remarks>
///     For the cases <c>MockFileSystem</c> cannot carry: it does not resolve traversal through a symbolic
///     link, so it yields nothing on the far side whether the guard under test is present or not. Every
///     other case belongs in <see cref="ContentFixture"/>, which reaches no disk and needs no cleanup.
///     Disposal removes the tree, so a test that fails still leaves nothing behind.
/// </remarks>
internal sealed class DiskFixture : IDisposable
{
    private DiskFixture(string root, SiteOptions options)
    {
        this.Root = root;
        this.Options = options;
        this.ContentRoot = Path.Combine(root, options.ContentDirectory);
        this.OutputRoot = Path.Combine(root, options.OutputDirectory);

        IFileSystem fileSystem = new FileSystem();
        HostEnvironmentStub environment = new() { ContentRootPath = root };

        this.Pipeline = new ContentPipeline(environment, fileSystem);

        this.Builder = new SiteBuilder(
            this.Pipeline,
            environment,
            new OptionsMonitorStub(options),
            fileSystem,
            new FakeLogger<SiteBuilder>());
    }

    /// <summary>
    ///     The temporary directory holding the whole fixture.
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
    ///     Discovers and renders the fixture's content.
    /// </summary>
    public ContentPipeline Pipeline { get; }

    /// <summary>
    ///     Generates the fixture's content into the fixture's output directory.
    /// </summary>
    public SiteBuilder Builder { get; }

    /// <summary>
    ///     Creates a fixture holding an empty content directory and an empty output directory.
    /// </summary>
    /// <returns>
    ///     The new fixture.
    /// </returns>
    public static DiskFixture Create()
    {
        SiteOptions options = new();
        DiskFixture fixture = new(Path.Combine(Path.GetTempPath(), $"gelyn-{Guid.NewGuid():N}"), options);

        Directory.CreateDirectory(fixture.ContentRoot);
        Directory.CreateDirectory(fixture.OutputRoot);

        return fixture;
    }

    /// <summary>
    ///     Adds one source file, creating the directories leading to it.
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
    public DiskFixture Write(string relativePath, string contents) =>
        this.WriteAt(this.ContentRoot, relativePath, contents);

    /// <summary>
    ///     Adds one file outside both the content and the output directory, where nothing the tool does may
    ///     reach it.
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
    public DiskFixture WriteOutside(string relativePath, string contents) =>
        this.WriteAt(this.Root, relativePath, contents);

    /// <summary>
    ///     Links a directory in the content tree to the content tree itself, which is the shape that makes a
    ///     walk unbounded. Any other link only makes it wider than intended.
    /// </summary>
    /// <param name="name">
    ///     The name the link is created under, directly below <see cref="ContentRoot"/>.
    /// </param>
    /// <returns>
    ///     The same fixture, so that calls can be chained.
    /// </returns>
    public DiskFixture LoopContentIntoItself(string name)
    {
        Directory.CreateSymbolicLink(Path.Combine(this.ContentRoot, name), this.ContentRoot);

        return this;
    }

    /// <summary>
    ///     Links a directory in the output tree to a directory outside it, which is the shape that would let
    ///     the reconcile delete a file the build never wrote.
    /// </summary>
    /// <param name="name">
    ///     The name the link is created under, directly below <see cref="OutputRoot"/>.
    /// </param>
    /// <param name="relativePath">
    ///     The path of the directory the link resolves to, relative to <see cref="Root"/>.
    /// </param>
    /// <returns>
    ///     The same fixture, so that calls can be chained.
    /// </returns>
    public DiskFixture LinkOutputOutside(string name, string relativePath)
    {
        Directory.CreateSymbolicLink(Path.Combine(this.OutputRoot, name), Path.Combine(this.Root, relativePath));

        return this;
    }

    /// <summary>
    ///     Indicates whether the fixture still holds a file at the given path.
    /// </summary>
    /// <param name="relativePath">
    ///     The path of the file relative to <see cref="Root"/>, separated by forward slashes.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> when the file is present, <see langword="false"/> otherwise.
    /// </returns>
    public bool Holds(string relativePath) => File.Exists(Locate(this.Root, relativePath));

    /// <summary>
    ///     Removes the whole tree, links included.
    /// </summary>
    public void Dispose()
    {
        // Tolerated, because a fixture whose creation failed part way leaves the test with the failure that
        // matters instead of with one from the cleanup.
        try
        {
            Directory.Delete(this.Root, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
    }

    #region Helpers

    private static string Locate(string root, string relativePath) =>
        Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private DiskFixture WriteAt(string root, string relativePath, string contents)
    {
        string path = Locate(root, relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);

        return this;
    }

    #endregion
}
