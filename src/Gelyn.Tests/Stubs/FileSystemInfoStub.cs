using System;
using System.IO;
using System.IO.Abstractions;
using System.Runtime.Versioning;

using Testably.Abstractions.Testing;

namespace Gelyn.Tests.Stubs;

/// <summary>
///     A file system entry whose every property is settable and which no storage backs, so that a test can hold
///     an entry in a state no file system produces on request.
/// </summary>
internal sealed class FileSystemInfoStub : IFileSystemInfo
{
    /// <inheritdoc/>
    public IFileSystem FileSystem { get; set; } = new MockFileSystem();

    /// <inheritdoc/>
    public FileAttributes Attributes { get; set; } = FileAttributes.Normal;

    /// <inheritdoc/>
    public DateTime CreationTime { get; set; }

    /// <inheritdoc/>
    public DateTime CreationTimeUtc { get; set; }

    /// <inheritdoc/>
    public bool Exists { get; set; } = true;

    /// <inheritdoc/>
    public string Extension { get; set; } = string.Empty;

    /// <inheritdoc/>
    public string FullName { get; set; } = string.Empty;

    /// <inheritdoc/>
    public DateTime LastAccessTime { get; set; }

    /// <inheritdoc/>
    public DateTime LastAccessTimeUtc { get; set; }

    /// <inheritdoc/>
    public DateTime LastWriteTime { get; set; }

    /// <inheritdoc/>
    public DateTime LastWriteTimeUtc { get; set; }

    /// <inheritdoc/>
    public string? LinkTarget { get; set; }

    /// <inheritdoc/>
    public string Name { get; set; } = string.Empty;

    /// <inheritdoc/>
    public UnixFileMode UnixFileMode { get; [UnsupportedOSPlatform("windows")] set; }

    /// <inheritdoc/>
    public void CreateAsSymbolicLink(string pathToTarget) =>
        throw new NotSupportedException();

    /// <inheritdoc/>
    public void Delete() =>
        throw new NotSupportedException();

    /// <inheritdoc/>
    public void Refresh() =>
        throw new NotSupportedException();

    /// <inheritdoc/>
    public IFileSystemInfo? ResolveLinkTarget(bool returnFinalTarget) =>
        throw new NotSupportedException();
}
