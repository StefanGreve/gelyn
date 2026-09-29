using System;

using Gelyn.Internals;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Gelyn.Extensions;

/// <summary>
///     The logging setup the composition root in <see cref="Program"/> applies.
/// </summary>
public static class LoggingBuilderExtensions
{
    private const string TimestampFormat = "HH:mm:ss.fff ";

    /// <summary>
    ///     Replaces the providers the host installs by default with a single console provider that writes to
    ///     standard error, at the level <c>--verbosity</c> selects.
    /// </summary>
    /// <remarks>
    ///     Diagnostics are kept off standard output because that stream carries the result of the command,
    ///     which <see cref="Spectre.Console.IAnsiConsole"/> writes and a caller may redirect or parse.
    ///     <see cref="ConsoleLoggerOptions.LogToStandardErrorThreshold"/> defaults to
    ///     <see cref="LogLevel.None"/>, which would put every record on standard output instead.
    /// </remarks>
    /// <param name="logging">
    ///     The builder to configure.
    /// </param>
    /// <param name="configuration">
    ///     Supplies the requested level, which is read from here rather than from the parsed command line
    ///     because the providers have to be configured before the host is built.
    /// </param>
    /// <returns>
    ///     The same <see cref="ILoggingBuilder"/> instance, so that calls can be chained.
    /// </returns>
    public static ILoggingBuilder ConfigureDiagnostics(this ILoggingBuilder logging, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(logging);
        ArgumentNullException.ThrowIfNull(configuration);

        // The option declares the same default, but the command line provider only reports what was passed.
        LogLevel level = Enum.TryParse(configuration[VerbosityOption.LevelKey], ignoreCase: true, out LogLevel parsed)
            ? parsed
            : VerbosityOption.DefaultLevel;

        logging.ClearProviders();
        logging.SetMinimumLevel(level);

        if (level == LogLevel.None)
            return logging;

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
