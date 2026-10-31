using System.CommandLine;

using Gelyn.Core.Internals;
using Gelyn.Model;
using Gelyn.Services;

using Microsoft.Extensions.DependencyInjection;

using Spectre.Console;

namespace Gelyn.Commands;

/// <summary>
///     Generates the site.
/// </summary>
internal sealed class BuildCommand : Command
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="BuildCommand"/> class.
    /// </summary>
    /// <param name="siteBuilder">
    ///     Performs the generation.
    /// </param>
    /// <param name="stdOut">
    ///     The console the command writes its output to.
    /// </param>
    /// <param name="stdErr">
    ///     The console the command writes its failures to.
    /// </param>
    public BuildCommand(
        SiteBuilder siteBuilder,
        [FromKeyedServices(ConsoleTarget.StdOut)] IAnsiConsole stdOut,
        [FromKeyedServices(ConsoleTarget.StdErr)] IAnsiConsole stdErr)
        : base("build", "Generate the site into the output directory.")
    {
        foreach (Option option in OverrideOptions.Definitions)
            this.Options.Add(option);

        this.SetAction(async (_, cancellationToken) =>
        {
            try
            {
                BuildReport report = await siteBuilder
                    .BuildAsync(cancellationToken)
                    .ConfigureAwait(false);

                foreach (string page in report.Pages)
                    stdOut.MarkupLineInterpolated($"[green]created[/] {page}");

                foreach (string file in report.Generated)
                    stdOut.MarkupLineInterpolated($"[green]created[/] {file}");

                foreach (string copy in report.Copies)
                    stdOut.MarkupLineInterpolated($"[green]copied[/] {copy}");

                foreach (string removal in report.Removed)
                    stdOut.MarkupLineInterpolated($"[yellow]removed[/] {removal}");

                // Formatted with Tool.Culture, because interpolating the value would use the current one.
                string elapsed = report.Elapsed.TotalMilliseconds.ToString("F2", Tool.Culture);
                stdOut.MarkupLineInterpolated($"Processed [bold]{report.FileCount}[/] file(s) in [bold]{elapsed}[/] ms.");

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
