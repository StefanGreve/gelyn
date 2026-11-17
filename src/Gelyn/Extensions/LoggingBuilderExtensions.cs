using Gelyn.Core.Internals;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Gelyn.Extensions;

/// <summary>
///     The logging setup the composition root in <see cref="Program"/> applies.
/// </summary>
internal static class LoggingBuilderExtensions
{
    private const string TimestampFormat = "HH:mm:ss.fff ";

    /// <summary>
    ///     Replaces the providers the host installs by default with a single console provider that writes to
    ///     standard error, at the level <c>--verbosity</c> selects.
    /// </summary>
    /// <param name="logging">
    ///     The builder to configure.
    /// </param>
    /// <param name="level">
    ///     The minimum level to write, as <see cref="VerbosityOption.ReadLevel"/> reads it.
    /// </param>
    /// <returns>
    ///     The same <see cref="ILoggingBuilder"/> instance, so that calls can be chained.
    /// </returns>
    public static ILoggingBuilder ConfigureDiagnostics(this ILoggingBuilder logging, LogLevel level)
    {
        ArgumentNullException.ThrowIfNull(logging);

        logging.ClearProviders();
        logging.SetMinimumLevel(level);

        if (level == LogLevel.None) return logging;

        // Standard output carries the command's result, and this threshold defaults to None, meaning stdout.
        logging.AddConsole(static options => options.LogToStandardErrorThreshold = LogLevel.Trace);

        logging.AddSimpleConsole(static options =>
        {
            options.SingleLine = true;
            options.ColorBehavior = LoggerColorBehavior.Default;
            options.TimestampFormat = TimestampFormat;
        });

        return logging;
    }
}
