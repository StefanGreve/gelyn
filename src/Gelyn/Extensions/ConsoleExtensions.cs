using Gelyn.Core.Internals.Terminal;
using Gelyn.Exceptions;

namespace Gelyn.Extensions;

/// <summary>
///     Provides extension methods for <see cref="ConsoleStream"/>.
/// </summary>
internal static class ConsoleExtensions
{
    /// <summary>
    ///     Defines extensions for the stream that is written to.
    /// </summary>
    /// <param name="console">
    ///     The stream, used as the receiver.
    /// </param>
    extension(ConsoleStream console)
    {
        /// <summary>
        ///     Writes one <c>error:</c> line for each failure an exception carries.
        /// </summary>
        /// <remarks>
        ///     A <see cref="ConfigurationException"/> is written by its failures, because its message joins them
        ///     on one line.
        /// </remarks>
        /// <param name="exception">
        ///     The exception to report.
        /// </param>
        public void WriteError(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            foreach (string message in CollectMessages(exception))
                console.WriteLine($"{console.Paint("error:", AnsiCodes.BrightRed)} {message}");
        }
    }

    #region Helpers

    private static IReadOnlyList<string> CollectMessages(Exception exception) => exception switch
    {
        ConfigurationException configuration => configuration.Failures,
        _ => [exception.Message],
    };

    #endregion
}
