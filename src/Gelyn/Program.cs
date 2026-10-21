using System.CommandLine;
using System.Diagnostics;
using System.IO.Abstractions;

using Gelyn.Commands;
using Gelyn.Core.Internals;
using Gelyn.Extensions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Spectre.Console;

namespace Gelyn;

/// <summary>
///     Entry point for the <c>gelyn</c> command line tool.
/// </summary>
internal static class Program
{
    /// <summary>
    ///     Runs the tool.
    /// </summary>
    /// <param name="args">
    ///     Command line arguments passed to the tool.
    /// </param>
    /// <returns>
    ///     Zero on success, a non-zero exit code otherwise.
    /// </returns>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = Justifications.ByDesign)]
    public static async Task<int> Main(string[] args)
    {
        long started = Stopwatch.GetTimestamp();
        IAnsiConsole console = AnsiConsole.Console;

        try
        {
            HostApplicationBuilder builder = args.CreateLeanApplicationBuilder();
            builder.Configuration.AddSiteConfiguration(args);
            builder.Logging.ConfigureDiagnostics(builder.Configuration);

            // Third-party registrations
            builder.Services.TryAddSingleton(console);
            builder.Services.TryAddSingleton<IFileSystem, FileSystem>();
            builder.Services.TryAddSingleton<IConfigurationRoot>(builder.Configuration);

            // First-party registrations
            builder.Services.TryAddSingleton(TimeProvider.System);
            builder.Services.AddSiteOptions();
            builder.Services.AddServices();
            builder.Services.AddCommands();

            using IHost host = builder.Build();

            // Ensures that --help and --version execute even when the configuration holds invalid values:
            // consumers read their options lazily, so validation surfaces here and only for a real command.
            // A configuration file that cannot be parsed is the exception, because loading it is eager.
            InvocationConfiguration configuration = new() { EnableDefaultExceptionHandler = false };

            ParseResult parsed = host.Services
                .GetRequiredService<GelynCommand>()
                .Parse(args);

            double startupMilliseconds = started.ElapsedMilliseconds;

            ILogger logger = host.Services
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger(typeof(Program));

            logger.LogStartupCompleted(startupMilliseconds);

            return await parsed
                .InvokeAsync(configuration)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            console.MarkupLineInterpolated($"[red]error:[/] {exception.Message}");

            return exception switch
            {
                OptionsValidationException or FileNotFoundException or InvalidDataException => ExitCodes.ConfigurationError,
                _ => ExitCodes.Error,
            };
        }
    }
}
