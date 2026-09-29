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
    /// <param name="logging">
    ///     The builder to configure.
    /// </param>
    /// <param name="configuration">
    ///     The configuration to read from.
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
