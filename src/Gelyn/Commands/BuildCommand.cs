using System.CommandLine;
using System.IO;

using Gelyn.Core.Internals;
using Gelyn.Model;
using Gelyn.Services;

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
    /// <param name="console">
    ///     The console the command writes its output to.
    /// </param>
    public BuildCommand(SiteBuilder siteBuilder, IAnsiConsole console)
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
                    console.MarkupLineInterpolated($"[green]created[/] {page}");

                foreach (string copy in report.Copies)
                    console.MarkupLineInterpolated($"[green]copied[/] {copy}");

                foreach (string removal in report.Removed)
                    console.MarkupLineInterpolated($"[yellow]removed[/] {removal}");

                // Formatted here rather than in the interpolation, so the culture is not left to the console.
                string elapsed = report.Elapsed.TotalMilliseconds.ToString("F2", Tool.Culture);
                console.MarkupLineInterpolated($"Processed [bold]{report.FileCount}[/] file(s) in [bold]{elapsed}[/] ms.");

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
