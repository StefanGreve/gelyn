using System;
using System.IO;

using Gelyn.Model;
using Gelyn.Services;

using Testably.Abstractions.Helpers;
using Testably.Abstractions.Testing;
using Testably.Abstractions.Testing.FileSystem;

namespace Gelyn.Tests.Fixtures;

/// <summary>
///     An in-memory output tree, together with the resolver that answers requests against it.
/// </summary>
/// <remarks>
///     The tree sits one level below a parent directory that holds nothing of the site, so that a test can
///     place a file the server must never reach and request it through a path that climbs out.
/// </remarks>
internal sealed class OutputFixture
{
    private const string RootDirectoryName = "_site";

    private OutputFixture(MockFileSystem fileSystem, string parent)
    {
        this.FileSystem = fileSystem;
        this.Parent = parent;
        this.Root = fileSystem.Path.Combine(parent, RootDirectoryName);
        this.Resolver = new StaticFileResolver(fileSystem);

        fileSystem.Directory.CreateDirectory(this.Root);
    }

    /// <summary>
    ///     The file system the resolver is wired to.
    /// </summary>
    public MockFileSystem FileSystem { get; }

    /// <summary>
    ///     The directory the generated site sits in, which is the only directory a request may reach.
    /// </summary>
    public string Root { get; }

    /// <summary>
    ///     The directory holding <see cref="Root"/>, which no request may reach.
    /// </summary>
    public string Parent { get; }

    /// <summary>
    ///     Resolves a request path against <see cref="Root"/>.
    /// </summary>
    public StaticFileResolver Resolver { get; }

    /// <summary>
    ///     Creates a fixture holding an output tree.
    /// </summary>
    /// <param name="relativePaths">
    ///     The paths of the files to write, relative to <see cref="Root"/> and separated by forward slashes.
    ///     Each one receives its own path as its contents, so that a test can tell the files apart by reading
    ///     what came back.
    /// </param>
    /// <returns>
    ///     The new fixture.
    /// </returns>
    public static OutputFixture Create(params string[] relativePaths)
    {
        var fileSystem = new MockFileSystem();
        string parent = fileSystem.Path.Combine(fileSystem.Path.GetTempPath(), "gelyn");

        var fixture = new OutputFixture(fileSystem, parent);

        foreach (string relativePath in relativePaths)
            fixture.Write(relativePath);

        return fixture;
    }

    /// <summary>
    ///     Adds one file to the output tree, creating the directories leading to it.
    /// </summary>
    /// <param name="relativePath">
    ///     The path of the file relative to <see cref="Root"/>, separated by forward slashes.
    /// </param>
    /// <param name="contents">
    ///     The exact text to write, which defaults to the path itself.
    /// </param>
    /// <returns>
    ///     The same fixture, so that calls can be chained.
    /// </returns>
    public OutputFixture Write(string relativePath, string? contents = null)
    {
        this.FileSystem.AddFile(this.Locate(relativePath), contents ?? relativePath);

        return this;
    }

    /// <summary>
    ///     Adds one file beside the output tree and not inside it, as a source directory or a neighboring
    ///     project would sit there.
    /// </summary>
    /// <param name="relativePath">
    ///     The path of the file relative to <see cref="Parent"/>, separated by forward slashes.
    /// </param>
    /// <returns>
    ///     The same fixture, so that calls can be chained.
    /// </returns>
    public OutputFixture WriteBeside(string relativePath)
    {
        this.FileSystem.AddFile(this.Resolve(this.Parent, relativePath), relativePath);

        return this;
    }

    /// <summary>
    ///     Reports the absolute path one output-relative path names, for an assertion about which file was
    ///     resolved.
    /// </summary>
    /// <param name="relativePath">
    ///     The path of the file relative to <see cref="Root"/>, separated by forward slashes.
    /// </param>
    /// <returns>
    ///     The absolute path.
    /// </returns>
    public string Locate(string relativePath) =>
        this.Resolve(this.Root, relativePath);

    /// <summary>
    ///     Resolves one request path against the output tree.
    /// </summary>
    /// <param name="requestPath">
    ///     The path of the requested URL, percent-encoded as a client would send it.
    /// </param>
    /// <returns>
    ///     What the request resolved to.
    /// </returns>
    public ServedFile Resolve(string requestPath) =>
        this.Resolver.Resolve(this.Root, requestPath);

    /// <summary>
    ///     Denies the next open of any file in the tree with an <see cref="UnauthorizedAccessException"/> and
    ///     grants every later one.
    /// </summary>
    /// <remarks>
    ///     The mock mirrors its host: it accepts an access control strategy on Windows only and a Unix file
    ///     mode strategy everywhere else. Both are consulted at the same point of an open, so the server sees
    ///     the same exception on every platform.
    /// </remarks>
    /// <returns>
    ///     The same fixture, so that calls can be chained.
    /// </returns>
    public OutputFixture DenyNextOpen()
    {
        bool denied = false;

        bool GrantAccess()
        {
            if (denied) return true;
            denied = true;
            return false;
        }

        if (OperatingSystem.IsWindows())
            this.FileSystem.WithAccessControlStrategy(new DefaultAccessControlStrategy((_, _, _) => GrantAccess()));
        else
            this.FileSystem.WithUnixFileModeStrategy(new CallbackUnixFileModeStrategy(GrantAccess));

        return this;
    }

    #region Helpers

    private string Resolve(string root, string relativePath) =>
        this.FileSystem.Path.Combine(root, relativePath.Replace('/', this.FileSystem.Path.DirectorySeparatorChar));

    private sealed class CallbackUnixFileModeStrategy(Func<bool> grant) : UnixFileModeStrategy
    {
        public override bool IsAccessGranted(string fullPath, IFileSystemExtensibility extensibility, UnixFileMode mode, AccessRequest request) =>
            grant();
    }

    #endregion
}
