using System.IO.Abstractions;

namespace Gelyn.Extensions;

/// <summary>
///     Extensions for a directory and for the entries a listing of it returns.
/// </summary>
internal static class FileSystemInfoExtensions
{
    /// <summary>
    ///     Defines extensions for a directory.
    /// </summary>
    /// <param name="directory">
    ///     The directory, used as the receiver.
    /// </param>
    extension(IDirectoryInfo directory)
    {
        /// <summary>
        ///     Lists the entries of the directory, files and directories alike, by name in ordinal order.
        /// </summary>
        /// <remarks>
        ///     Ordinal is the one order every file system and every culture agree on, so the scan, the prune and
        ///     the lines they report read the same everywhere. The entries are listed rather than enumerated,
        ///     because the prune deletes as it walks, and POSIX leaves it unspecified whether a directory stream
        ///     still returns an entry removed after the stream was opened:
        ///     https://pubs.opengroup.org/onlinepubs/9799919799/functions/readdir.html
        /// </remarks>
        /// <returns>
        ///     The entries, in ordinal order of their names.
        /// </returns>
        public IFileSystemInfo[] ListEntries() =>
            [.. directory.EnumerateFileSystemInfos().OrderBy(static entry => entry.Name, StringComparer.Ordinal)];
    }

    /// <summary>
    ///     Defines extensions for an entry of a directory listing.
    /// </summary>
    /// <param name="entry">
    ///     The file or directory, used as the receiver.
    /// </param>
    extension(IFileSystemInfo entry)
    {
        /// <summary>
        ///     Indicates whether the entry is a symbolic link or a junction.
        /// </summary>
        /// <remarks>
        ///     Besides symbolic links and junctions, Windows marks other entries as reparse points, such as a file
        ///     that OneDrive manages. They have no <see cref="IFileSystemInfo.LinkTarget"/> and are not links.
        ///     Only a reparse point has its link target read, which costs further file system calls.
        /// </remarks>
        public bool IsLink =>
            entry.Attributes.HasFlag(FileAttributes.ReparsePoint) && entry.LinkTarget is not null;
    }
}
