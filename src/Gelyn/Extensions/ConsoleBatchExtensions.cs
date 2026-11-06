using System.Globalization;
using System.Text;

using Spectre.Console;

namespace Gelyn.Extensions;

/// <summary>
///     Output that reaches a console in one write instead of one write per segment.
/// </summary>
internal static class ConsoleBatchExtensions
{
    /// <summary>
    ///     Defines extensions for the console a batch is written to.
    /// </summary>
    /// <param name="console">
    ///     The console, used as the receiver.
    /// </param>
    extension(IAnsiConsole console)
    {
        /// <summary>
        ///     Renders everything <paramref name="write"/> writes into memory, and passes the result to the
        ///     console's writer at once.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         The console flushes every segment it writes and queries its size for every render, and each
        ///         query throws and catches an <see cref="IOException"/> when no standard handle is a console.
        ///         The batch answers those queries with the size read once up front, so lines wrap as they
        ///         would have.
        ///     </para>
        ///     <para>
        ///         A terminal without escape sequences is written to directly, because that console colors its
        ///         text through the console API between writes, which deferred text would no longer line up
        ///         with. Nothing else may write to the console while the batch is open.
        ///     </para>
        /// </remarks>
        /// <param name="write">
        ///     Writes the batch to the console it is given.
        /// </param>
        public void WriteBatch(Action<IAnsiConsole> write)
        {
            ArgumentNullException.ThrowIfNull(write);

            Profile profile = console.Profile;
            IAnsiConsoleOutput output = profile.Out;

            if (output.IsTerminal && !profile.Capabilities.Ansi)
            {
                write(console);
                return;
            }

            using var buffer = new StringWriter(CultureInfo.InvariantCulture);
            profile.Out = new BufferedOutput(buffer, profile.Width, profile.Height);

            try
            {
                write(console);
            }
            finally
            {
                profile.Out = output;
                output.Writer.Write(buffer.GetStringBuilder());
            }
        }
    }

    /// <summary>
    ///     Stands in for the console's own output while a batch is open.
    /// </summary>
    /// <param name="writer">
    ///     Collects the batch.
    /// </param>
    /// <param name="width">
    ///     The console width the batch wraps at.
    /// </param>
    /// <param name="height">
    ///     The console height the batch renders against.
    /// </param>
    private sealed class BufferedOutput(TextWriter writer, int width, int height) : IAnsiConsoleOutput
    {
        /// <inheritdoc/>
        public TextWriter Writer => writer;

        /// <inheritdoc/>
        public bool IsTerminal => false;

        /// <inheritdoc/>
        public int Width => width;

        /// <inheritdoc/>
        public int Height => height;

        /// <inheritdoc/>
        public void SetEncoding(Encoding encoding)
        {
        }
    }
}
