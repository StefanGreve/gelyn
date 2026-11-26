using System.Buffers;
using System.IO.Abstractions;
using System.Text;

namespace Gelyn.Extensions;

/// <summary>
///     Extensions for the file operations of a file system.
/// </summary>
internal static class FileContentExtensions
{
    /// <summary>
    ///     Defines extensions for the file operations of a file system.
    /// </summary>
    /// <param name="file">
    ///     The file operations of the file system the files belong to, used as the receiver.
    /// </param>
    extension(IFile file)
    {
        /// <summary>
        ///     Copies the contents of one file to another, creating or overwriting the destination, and stamps
        ///     the copy with the given modification time.
        /// </summary>
        /// <remarks>
        ///     Unlike <see cref="IFile.Copy(string, string, bool)"/>, this leaves the attributes and permissions
        ///     of the source behind, so a read-only source does not produce a read-only copy that the next build
        ///     cannot overwrite. The caller supplies the modification time, so the source is not queried for it,
        ///     and a deploy tool that compares times still skips an unchanged file.
        /// </remarks>
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
        public void CopyContent(string sourcePath, string destinationPath, DateTime lastWriteTimeUtc)
        {
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
        ///     Reads a text file whole, with one read into a pooled buffer and one decode, and returns the same
        ///     text as <see cref="IFile.ReadAllText(string)"/>.
        /// </summary>
        /// <param name="path">
        ///     The path of the file to read.
        /// </param>
        /// <returns>
        ///     The text of the file without its byte order mark, with U+FFFD in place of every byte sequence the
        ///     detected encoding does not allow.
        /// </returns>
        public string ReadText(string path)
        {
            // Unbuffered, because the whole file goes into a buffer of its own length.
            using Stream stream = file.Open(path, new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read,
                Options = FileOptions.SequentialScan,
                BufferSize = 0,
            });

            // One byte more than the file holds, so that the read which finds the end needs no larger buffer.
            byte[] buffer = ArrayPool<byte>.Shared.Rent(checked((int)stream.Length + 1));
            int count = 0;

            try
            {
                while (true)
                {
                    // Full only if the file grew after its length was taken, as when an editor saves it during
                    // a rebuild.
                    if (count == buffer.Length)
                    {
                        byte[] larger = ArrayPool<byte>.Shared.Rent(buffer.Length * 2);
                        buffer.AsSpan(0, count).CopyTo(larger);
                        ArrayPool<byte>.Shared.Return(buffer);
                        buffer = larger;
                    }

                    int read = stream.Read(buffer, count, buffer.Length - count);

                    if (read == 0)
                        break;

                    count += read;
                }

                return DecodeText(buffer.AsSpan(0, count));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    #region Helpers

    // The marks StreamReader detects, the UTF-32 LE mark ahead of the UTF-16 LE mark it starts with. Encoding.UTF8
    // and not Tool.Encoding, which would turn a stray byte in a source into a failed build.
    private static string DecodeText(ReadOnlySpan<byte> bytes) => bytes switch
    {
        [0xEF, 0xBB, 0xBF, ..] => Encoding.UTF8.GetString(bytes[3..]),
        [0xFF, 0xFE, 0x00, 0x00, ..] => Encoding.UTF32.GetString(bytes[4..]),
        [0xFF, 0xFE, ..] => Encoding.Unicode.GetString(bytes[2..]),
        [0xFE, 0xFF, ..] => Encoding.BigEndianUnicode.GetString(bytes[2..]),
        [0x00, 0x00, 0xFE, 0xFF, ..] => new UTF32Encoding(bigEndian: true, byteOrderMark: true).GetString(bytes[4..]),
        _ => Encoding.UTF8.GetString(bytes),
    };

    #endregion
}
