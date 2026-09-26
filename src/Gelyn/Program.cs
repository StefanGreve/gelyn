using System;
using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;

using Gelyn.Commands;
using Gelyn.Extensions;
using Gelyn.Internals;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Spectre.Console;

namespace Gelyn;

/// <summary>
///     Entry point for the <c>gelyn</c> command line tool.
/// </summary>
public static class Program
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
        IAnsiConsole console = AnsiConsole.Console;

        try
        {
            HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
            AddConfigurationFile(builder.Configuration, args);

            // Third-party registrations
            builder.Services.TryAddSingleton(console);

            // First-party registrations
            builder.Services.AddSiteOptions();
            builder.Services.AddServices();
            builder.Services.AddCommands();

            using IHost host = builder.Build();

            // Ensures that --help and --version execute even when the configuration holds invalid values:
            // consumers read their options lazily, so validation surfaces here and only for a real command.
            // A configuration file that cannot be parsed is the exception, because loading it is eager.
            InvocationConfiguration configuration = new() { EnableDefaultExceptionHandler = false };

            return await host.Services
                .GetRequiredService<GelynCommand>()
                .Parse(args)
                .InvokeAsync(configuration)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            console.MarkupLineInterpolated($"[red]error:[/] {exception.Message}");

            return exception switch
            {
                OptionsValidationException or FileNotFoundException or InvalidDataException
                    => ExitCodes.ConfigurationError,
                _ => ExitCodes.Error,
            };
        }
    }

    #region Helpers

    private static void AddConfigurationFile(ConfigurationManager configuration, string[] args)
    {
        string? requested = configuration[ConfigurationFile.PathKey];

        configuration.AddJsonFile(
            requested ?? ConfigurationFile.DefaultFileName,
            optional: requested is null,
            reloadOnChange: true);

        configuration
            .AddEnvironmentVariables()
            .AddCommandLine(args);
    }

    #endregion
}
