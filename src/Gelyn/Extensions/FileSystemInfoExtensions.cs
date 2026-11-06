using System.IO.Abstractions;

namespace Gelyn.Extensions;

/// <summary>
///     Classifications of the entries a directory enumeration returns.
/// </summary>
internal static class FileSystemInfoExtensions
{
    /// <summary>
    ///     Defines extensions for an entry of a directory enumeration.
    /// </summary>
    /// <param name="entry">
    ///     The file or directory, used as the receiver.
    /// </param>
    extension(IFileSystemInfo entry)
    {
        /// <summary>
        ///     Whether the entry is a symbolic link or a junction.
        /// </summary>
        /// <remarks>
        ///     The attribute comes with the enumeration and so costs nothing, whereas reading
        ///     <see cref="IFileSystemInfo.LinkTarget"/> opens the entry on Windows and reads its link on Unix.
        ///     The target still decides, because a reparse point of any other kind has none.
        /// </remarks>
        public bool IsLink =>
            entry.Attributes.HasFlag(FileAttributes.ReparsePoint) && entry.LinkTarget is not null;
    }
}
