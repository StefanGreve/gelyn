using System;
using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

using Gelyn.Commands;
using Gelyn.Extensions;
using Gelyn.Internals;

using Microsoft.Extensions.DependencyInjection;
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
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddGelyn();

        using IHost host = builder.Build();

        try
        {
            // Ensures that --help and --version execute regardless of the configuration state: consumers
            // read their options lazily, so validation only surfaces here, and only for a real command.
            InvocationConfiguration configuration = new() { EnableDefaultExceptionHandler = false };

            return await host.Services
                .GetRequiredService<GelynCommand>()
                .Parse(args)
                .InvokeAsync(configuration)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            host.Services
                .GetRequiredService<IAnsiConsole>()
                .MarkupLineInterpolated($"[red]error:[/] {exception.Message}");

            return exception switch
            {
                OptionsValidationException => ExitCodes.ConfigurationError,
                _ => ExitCodes.Error,
            };
        }
    }
}