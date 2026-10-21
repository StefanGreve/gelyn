namespace Gelyn.Core.Internals;

/// <summary>
///     Resolves one path against a directory and reports whether the result stayed inside it.
/// </summary>
/// <remarks>
///     No member requires a path to exist, so one that leaves the directory it was resolved against can be
///     rejected before anything opens it.
/// </remarks>
internal static class PathBoundary
{
    /// <summary>
    ///     Resolves one path to its absolute form.
    /// </summary>
    /// <param name="path">
    ///     The path to resolve, absolute or relative to the working directory.
    /// </param>
    /// <returns>
    ///     The absolute path, with any trailing separator removed so that two results compare as text.
    /// </returns>
    /// <exception cref="ArgumentException">
    ///     <paramref name="path"/> is empty or carries a character that no path may hold, such as NUL.
    /// </exception>
    internal static string Resolve(string path) => Normalize(Path.GetFullPath(path));

    /// <summary>
    ///     Resolves one path against a directory, collapsing the <c>..</c> segments either of them holds.
    /// </summary>
    /// <param name="root">
    ///     The directory a relative <paramref name="path"/> is resolved against.
    /// </param>
    /// <param name="path">
    ///     The path to resolve. A rooted value replaces <paramref name="root"/> rather than extending it, and
    ///     a relative one can still climb above it, which is why <see cref="Contains"/> has to be consulted
    ///     before the result is used for anything.
    /// </param>
    /// <returns>
    ///     The absolute path, with any trailing separator removed so that two results compare as text.
    /// </returns>
    /// <exception cref="ArgumentException">
    ///     The combination of <paramref name="root"/> and <paramref name="path"/> is empty or carries a
    ///     character that no path may hold, such as NUL.
    /// </exception>
    internal static string Resolve(string root, string path) => Normalize(Path.GetFullPath(Path.Combine(root, path)));

    /// <summary>
    ///     Indicates whether one path is a directory itself or sits below it.
    /// </summary>
    /// <remarks>
    ///     Compared case sensitively, because the only paths this has to accept are the ones a
    ///     <c>Resolve</c> overload built by appending to <paramref name="directory"/> verbatim. Disregarding
    ///     case would additionally accept a path that climbed out of the directory and back into a sibling
    ///     spelled differently, which on a case-sensitive file system is somewhere else entirely.
    /// </remarks>
    /// <param name="directory">
    ///     The containing directory, as a <c>Resolve</c> overload returned it.
    /// </param>
    /// <param name="path">
    ///     The path to test, as a <c>Resolve</c> overload returned it.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> when <paramref name="path"/> is <paramref name="directory"/> or sits below
    ///     it, <see langword="false"/> otherwise.
    /// </returns>
    internal static bool Contains(string directory, string path) =>
        path.Equals(directory, StringComparison.Ordinal)
        || path.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    /// <summary>
    ///     Indicates whether either of two paths is the other or sits below it.
    /// </summary>
    /// <remarks>
    ///     Compared case insensitively, which is the safe direction for a test whose purpose is to reject an
    ///     overlap: two settings naming one directory on a case-insensitive file system are reported as the
    ///     overlap they are, at the cost of also reporting two directories that only a case-sensitive file
    ///     system keeps apart. <see cref="Contains"/> is the directional test and compares exactly.
    /// </remarks>
    /// <param name="first">
    ///     One path, as a <c>Resolve</c> overload returned it.
    /// </param>
    /// <param name="second">
    ///     The other path, as a <c>Resolve</c> overload returned it.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> when the two paths name one directory or one of them sits below the other,
    ///     <see langword="false"/> otherwise.
    /// </returns>
    internal static bool Overlap(string first, string second) =>
        first.Equals(second, StringComparison.OrdinalIgnoreCase)
        || first.StartsWith(second + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
        || second.StartsWith(first + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    #region Helpers

    private static string Normalize(string path) =>
        path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    #endregion
}
