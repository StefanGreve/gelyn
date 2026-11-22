using System.IO;

using Gelyn.Core.Internals;

namespace Gelyn.Tests.Fixtures;

/// <summary>
///     The stream a harness captures the output of.
/// </summary>
internal static class CapturedConsole
{
    /// <summary>
    ///     Creates a stream whose output is plain text, as a redirected stream's is, whatever environment the
    ///     suite runs in.
    /// </summary>
    /// <param name="writer">
    ///     Receives everything the stream writes.
    /// </param>
    /// <returns>
    ///     The new stream.
    /// </returns>
    public static ConsoleStream Create(TextWriter writer) =>
        new(writer, isTerminal: false, useColors: false);
}
