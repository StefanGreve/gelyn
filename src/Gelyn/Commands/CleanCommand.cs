using System.CommandLine;
using System.IO.Abstractions;

using Gelyn.Core.Internals;
using Gelyn.Model.Options;

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
    /// <param name="console">
    ///     The console the command writes its output to.
    /// </param>
    public CleanCommand(
        IHostEnvironment environment,
        IOptionsMonitor<SiteOptions> options,
        IFileSystem fileSystem,
        IAnsiConsole console)
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
                    console.MarkupLineInterpolated($"Nothing to remove at {directory}.");

                    return ExitCodes.Success;
                }

                fileSystem.Directory.Delete(directory, recursive: true);
                console.MarkupLineInterpolated($"[green]removed[/] {directory}");

                return ExitCodes.Success;
            }
            catch (IOException exception)
            {
                console.MarkupLineInterpolated($"[red]error:[/] {exception.Message}");

                return ExitCodes.Error;
            }
        });
    }
}
