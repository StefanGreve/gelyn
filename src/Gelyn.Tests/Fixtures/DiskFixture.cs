using System;
using System.Collections.Generic;
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
    private readonly List<string> _readOnly = [];

    private DiskFixture(string root, SiteOptions options)
    {
        this.Root = root;
        this.Options = options;
        this.ContentRoot = Path.Combine(root, options.ContentDirectory);
        this.OutputRoot = Path.Combine(root, options.OutputDirectory);

        IFileSystem fileSystem = new FileSystem();
        var environment = new HostEnvironmentStub { ContentRootPath = root };

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
    ///     Indicates whether the current process may create the directory links
    ///     <see cref="LoopContentIntoItself"/> and <see cref="LinkOutputOutside"/> make.
    /// </summary>
    /// <remarks>
    ///     Any process may on macOS and Linux. Windows reserves the right to administrators, so a test needs an
    ///     elevated shell there. Developer Mode lifts that restriction as well, but this check does not detect it,
    ///     so a non-elevated test skips on such a machine even though it could run.
    /// </remarks>
    public static bool CanLinkDirectories =>
        !OperatingSystem.IsWindows() || Environment.IsPrivilegedProcess;

    /// <summary>
    ///     Indicates whether the disk the fixtures are created on resolves a name without regard to case, as NTFS
    ///     and APFS do by default.
    /// </summary>
    /// <remarks>
    ///     Probed with a file in the temporary directory, because case sensitivity belongs to the volume, and on
    ///     Windows and Linux even to the directory, so the operating system alone does not settle it.
    /// </remarks>
    public static bool IgnoresCase { get; } = ProbeCase();

    /// <summary>
    ///     Creates a fixture holding an empty content directory and an empty output directory.
    /// </summary>
    /// <returns>
    ///     The new fixture.
    /// </returns>
    public static DiskFixture Create()
    {
        var options = new SiteOptions();
        var fixture = new DiskFixture(Path.Combine(Path.GetTempPath(), $"gelyn-{Guid.NewGuid():N}"), options);

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
    ///     Adds one source file and marks it read-only, as a checkout from a server workspace or an extracted
    ///     archive would leave it.
    /// </summary>
    /// <param name="relativePath">
    ///     The path of the file relative to <see cref="ContentRoot"/>, separated by forward slashes.
    /// </param>
    /// <param name="contents">
    ///     The text to write.
    /// </param>
    /// <returns>
    ///     The same fixture, so that calls can be chained.
    /// </returns>
    public DiskFixture WriteReadOnly(string relativePath, string contents)
    {
        this.WriteAt(this.ContentRoot, relativePath, contents);

        string path = Locate(this.ContentRoot, relativePath);
        File.SetAttributes(path, FileAttributes.ReadOnly);
        this._readOnly.Add(path);

        return this;
    }

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
    ///     the prune delete a file the build never wrote.
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
    ///     Replaces the output directory with a link to the content directory, which is the shape that would let
    ///     the build write its pages among the sources and the prune delete them.
    /// </summary>
    /// <returns>
    ///     The same fixture, so that calls can be chained.
    /// </returns>
    public DiskFixture LinkOutputToContent()
    {
        Directory.Delete(this.OutputRoot);
        Directory.CreateSymbolicLink(this.OutputRoot, this.ContentRoot);

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
    public bool Holds(string relativePath) =>
        File.Exists(Locate(this.Root, relativePath));

    /// <summary>
    ///     Removes the whole tree, links included.
    /// </summary>
    public void Dispose()
    {
        // Tolerated, because a fixture whose creation failed part way, or a file Windows still holds open for a
        // moment, leaves the test with the failure that matters instead of with one from the cleanup.
        try
        {
            // Cleared first, because Windows refuses to delete a read-only file.
            foreach (string path in this._readOnly)
                if (File.Exists(path)) File.SetAttributes(path, FileAttributes.Normal);

            Directory.Delete(this.Root, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    #region Helpers

    private static bool ProbeCase()
    {
        string name = $"gelyn-{Guid.NewGuid():N}";
        string path = Path.Combine(Path.GetTempPath(), name);

        File.WriteAllText(path, string.Empty);

        try
        {
            return File.Exists(Path.Combine(Path.GetTempPath(), name.ToUpperInvariant()));
        }
        finally
        {
            File.Delete(path);
        }
    }

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
