using System.CommandLine;
using System.IO.Abstractions;

using Gelyn.Core.Internals;
using Gelyn.Model.Options;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Spectre.Console;

namespace Gelyn.Commands;

/// <summary>
///     Removes the generated site.
/// </summary>
internal sealed class CleanCommand : Command
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="CleanCommand"/> class.
    /// </summary>
    /// <param name="environment">
    ///     The host environment relative paths are resolved against.
    /// </param>
    /// <param name="options">
    ///     Supplies the site configuration naming the directory to remove.
    /// </param>
    /// <param name="fileSystem">
    ///     The file system to work against.
    /// </param>
    /// <param name="stdOut">
    ///     The console the command writes its output to.
    /// </param>
    /// <param name="stdErr">
    ///     The console the command writes its failures to.
    /// </param>
    public CleanCommand(
        IHostEnvironment environment,
        IOptionsMonitor<SiteOptions> options,
        IFileSystem fileSystem,
        [FromKeyedServices(ConsoleTarget.StdOut)] IAnsiConsole stdOut,
        [FromKeyedServices(ConsoleTarget.StdErr)] IAnsiConsole stdErr)
        : base("clean", "Remove the output directory.")
    {
        this.SetAction(_ =>
        {
            SiteOptions site = options.CurrentValue;
            string directory = fileSystem.Path.Combine(environment.ContentRootPath, site.OutputDirectory);

            try
            {
                if (!fileSystem.Directory.Exists(directory))
                {
                    stdOut.MarkupLineInterpolated($"Nothing to remove at {directory}.");

                    return ExitCodes.Success;
                }

                fileSystem.Directory.Delete(directory, recursive: true);
                stdOut.MarkupLineInterpolated($"[green]removed[/] {directory}");

                return ExitCodes.Success;
            }
            catch (IOException exception)
            {
                stdErr.MarkupLineInterpolated($"[red]error:[/] {exception.Message}");

                return ExitCodes.Error;
            }
        });
    }
}
