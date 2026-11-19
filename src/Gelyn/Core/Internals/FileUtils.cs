using System.Buffers;
using System.IO.Abstractions;
using System.Text;

namespace Gelyn.Core.Internals;

/// <summary>
///     File operations the build needs in a form <see cref="IFile"/> does not offer directly.
/// </summary>
internal static class FileUtils
{
    // The encoding File.WriteAllText writes with, so that the bytes on disk stay the same.
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    ///     Copies the contents of one file to another, creating or overwriting the destination, and stamps the
    ///     copy with the given modification time.
    /// </summary>
    /// <remarks>
    ///     Unlike <see cref="IFile.Copy(string, string, bool)"/>, this leaves the attributes and permissions of
    ///     the source behind, so a read-only source does not produce a read-only copy that the next build cannot
    ///     overwrite. The caller supplies the modification time, so the source is not queried for it, and a deploy
    ///     tool that compares times still skips an unchanged file.
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
    /// <param name="lastWriteTimeUtc">
    ///     The modification time of the copy, in UTC, normally the one the source was enumerated with.
    /// </param>
    /// <exception cref="UnauthorizedAccessException">
    ///     <paramref name="destinationPath"/> names an existing file that is read-only.
    /// </exception>
    internal static void CopyContent(IFileSystem fileSystem, string sourcePath, string destinationPath, DateTime lastWriteTimeUtc)
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
        file.SetLastWriteTimeUtc(destinationPath, lastWriteTimeUtc);
    }

    /// <summary>
    ///     Writes text to a file as UTF-8 without a byte order mark, creating or overwriting the file.
    /// </summary>
    /// <remarks>
    ///     The text is encoded into one pooled buffer and written in a single call, where
    ///     <see cref="IFile.WriteAllText(string, string)"/> writes 8,192 characters per call and preallocates a
    ///     file it expects to grow past that.
    /// </remarks>
    /// <param name="fileSystem">
    ///     The file system the file belongs to.
    /// </param>
    /// <param name="path">
    ///     The path of the file, whose directory must exist.
    /// </param>
    /// <param name="text">
    ///     The text to write.
    /// </param>
    /// <exception cref="EncoderFallbackException">
    ///     <paramref name="text"/> holds an unpaired surrogate, in which case the file is left as it was.
    /// </exception>
    internal static void WriteUtf8(IFileSystem fileSystem, string path, string text)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(Utf8.GetByteCount(text));

        try
        {
            int length = Utf8.GetBytes(text, buffer);
            fileSystem.File.WriteAllBytes(path, buffer.AsSpan(0, length));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
