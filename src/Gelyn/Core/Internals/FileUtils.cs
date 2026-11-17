using System.IO.Abstractions;

namespace Gelyn.Core.Internals;

/// <summary>
///     File operations the build needs in a form <see cref="IFile"/> does not offer directly.
/// </summary>
internal static class FileUtils
{
    /// <summary>
    ///     Copies the contents and the modification time of one file to another, creating or overwriting the
    ///     destination.
    /// </summary>
    /// <remarks>
    ///     Unlike <see cref="IFile.Copy(string, string, bool)"/>, this leaves the attributes and permissions of
    ///     the source behind, so a read-only source does not produce a read-only copy that the next build cannot
    ///     overwrite. The modification time is carried over, so a deploy tool that compares it still skips an
    ///     unchanged file.
    /// </remarks>
    /// <param name="fileSystem">
    ///     The file system both files belong to.
    /// </param>
    /// <param name="sourcePath">
    ///     The path of the file to copy.
    /// </param>
    /// <param name="destinationPath">
    ///     The path of the copy, whose directory must exist.
    /// </param>
    /// <exception cref="UnauthorizedAccessException">
    ///     <paramref name="destinationPath"/> names an existing file that is read-only.
    /// </exception>
    internal static void CopyContent(IFileSystem fileSystem, string sourcePath, string destinationPath)
    {
        IFile file = fileSystem.File;

        // Unbuffered on both ends, because CopyTo brings its own buffer and a second one only adds a copy.
        using (Stream source = file.Open(sourcePath, new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read,
            Options = FileOptions.SequentialScan,
            BufferSize = 0,
        }))
        using (Stream destination = file.Open(destinationPath, new FileStreamOptions
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
            PreallocationSize = source.Length,
            BufferSize = 0,
        }))
        {
            source.CopyTo(destination);
        }

        // Set once the destination is closed, since closing it would otherwise stamp the time of the write.
        file.SetLastWriteTimeUtc(destinationPath, file.GetLastWriteTimeUtc(sourcePath));
    }
}
