using System.CommandLine;
using System.Diagnostics;
using System.IO.Abstractions;

using Gelyn.Commands;
using Gelyn.Core.Internals;
using Gelyn.Diagnostics;
using Gelyn.Extensions;
using Gelyn.Model;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Spectre.Console;

using Testably.Abstractions;

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

        IAnsiConsole stdOut = AnsiConsole.Console;
        IAnsiConsole stdErr = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(System.Console.Error),
        });

        stdOut.DisableWrappingWhenRedirected();
        stdErr.DisableWrappingWhenRedirected();

        try
        {
            HostApplicationBuilder builder = HostExtensions.CreateLeanApplicationBuilder();
            ConfigurationFileStatus file = builder.Configuration.AddSiteConfiguration(ConfigurationFile.ReadPath(args));
            builder.Logging.ConfigureDiagnostics(VerbosityOption.ReadLevel(args));

            // Third-party registrations
            builder.Services.TryAddKeyedSingleton(ConsoleTarget.StdOut, stdOut);
            builder.Services.TryAddKeyedSingleton(ConsoleTarget.StdErr, stdErr);
            builder.Services.TryAddSingleton<IFileSystem, RealFileSystem>();
            builder.Services.TryAddSingleton<IConfigurationRoot>(builder.Configuration);

            // First-party registrations
            builder.Services.TryAddSingleton(TimeProvider.System);
            builder.Services.AddConfigurationFile(file);
            builder.Services.AddSiteOptions();
            builder.Services.AddServices();
            builder.Services.AddCommands();

            using IHost host = builder.Build();

            // Lets a command's exception reach the catch block below, which writes one error line per problem and
            // picks the exit code. The default handler would print the stack trace and return 1.
            var configuration = new InvocationConfiguration { EnableDefaultExceptionHandler = false };

            ParseResult parsed = host.Services
                .GetRequiredService<GelynCommand>()
                .Parse(args);

            builder.Configuration.ApplyOverrides(parsed);

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
            stdErr.WriteError(exception);
            return ExitCodes.For(exception);
        }
    }
}
