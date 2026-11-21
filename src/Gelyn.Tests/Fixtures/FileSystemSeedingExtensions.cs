using System;
using System.IO.Abstractions;

namespace Gelyn.Tests.Fixtures;

/// <summary>
///     Seeding helpers for the file system a test arranges.
/// </summary>
internal static class FileSystemSeedingExtensions
{
    /// <summary>
    ///     Defines extensions for the file system a test arranges.
    /// </summary>
    /// <param name="fileSystem">
    ///     The file system to seed, used as the receiver.
    /// </param>
    extension(IFileSystem fileSystem)
    {
        /// <summary>
        ///     Writes one text file, creating the directories leading to it.
        /// </summary>
        /// <param name="path">
        ///     The path of the file.
        /// </param>
        /// <param name="contents">
        ///     The exact text to write.
        /// </param>
        public void AddFile(string path, string contents)
        {
            fileSystem.Directory.CreateDirectory(ParentOf(fileSystem, path));
            fileSystem.File.WriteAllText(path, contents);
        }

        /// <summary>
        ///     Writes one binary file, creating the directories leading to it.
        /// </summary>
        /// <param name="path">
        ///     The path of the file.
        /// </param>
        /// <param name="contents">
        ///     The exact bytes to write.
        /// </param>
        public void AddFile(string path, byte[] contents)
        {
            fileSystem.Directory.CreateDirectory(ParentOf(fileSystem, path));
            fileSystem.File.WriteAllBytes(path, contents);
        }
    }

    private static string ParentOf(IFileSystem fileSystem, string path) =>
        fileSystem.Path.GetDirectoryName(fileSystem.Path.GetFullPath(path))
            ?? throw new ArgumentException("The path names a root, which cannot hold a file.", nameof(path));
}
