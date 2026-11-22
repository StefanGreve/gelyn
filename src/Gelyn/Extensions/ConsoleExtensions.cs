using Gelyn.Core.Internals;

using Microsoft.Extensions.Options;

using Spectre.Console;

namespace Gelyn.Extensions;

/// <summary>
///     Provides extension methods for <see cref="IAnsiConsole"/>.
/// </summary>
internal static class ConsoleExtensions
{
    /// <summary>
    ///     Defines extensions for the console that is written to.
    /// </summary>
    /// <param name="console">
    ///     The console, used as the receiver.
    /// </param>
    extension(IAnsiConsole console)
    {
        /// <summary>
        ///     Stops the console from wrapping lines when its output is not a terminal.
        /// </summary>
        /// <remarks>
        ///     Spectre.Console assumes 80 columns for an output that is not a terminal, which splits long paths
        ///     in a file or a CI log. Left-aligned text renders without padding, so the unbounded width costs
        ///     nothing, but a widget that fills the width, such as a rule or a centered paragraph, would allocate
        ///     it.
        /// </remarks>
        public void DisableWrappingWhenRedirected()
        {
            if (!console.Profile.Out.IsTerminal) console.Profile.Width = int.MaxValue;
        }

        /// <summary>
        ///     Writes one <c>error:</c> line for each failure an exception carries.
        /// </summary>
        /// <remarks>
        ///     A <see cref="ConfigurationException"/> and an <see cref="OptionsValidationException"/> are written
        ///     by their failures, because their messages join them on one line. An <see cref="AggregateException"/>
        ///     is written by its contents, because its own message nests one "One or more errors occurred" per
        ///     layer and names none of the failures underneath them.
        /// </remarks>
        /// <param name="exception">
        ///     The exception to report.
        /// </param>
        public void WriteError(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            foreach (string message in CollectMessages(exception))
                console.MarkupLineInterpolated($"[red]error:[/] {message}");
        }
    }

    #region Helpers

    private static IEnumerable<string> CollectMessages(Exception exception) => exception switch
    {
        AggregateException aggregate => aggregate.InnerExceptions.SelectMany(CollectMessages),
        ConfigurationException configuration => configuration.Failures,
        OptionsValidationException validation => validation.Failures,
        _ => [exception.Message],
    };

    #endregion
}
