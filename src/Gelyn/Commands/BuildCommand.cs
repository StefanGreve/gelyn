using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using System.IO;

using Gelyn.Internals;
using Gelyn.Services;

using Spectre.Console;

namespace Gelyn.Commands;

/// <summary>
///     Generates the site.
/// </summary>
[SuppressMessage("Performance", "CA1812", Justification = Justifications.ByDesign)]
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
        // Never read here: the values reach SiteOptions through AddSiteConfiguration.
        this.Options.Add(new Option<string>($"--{OverrideOptions.Title}")
        {
            Description = "The name shown in the header and the footer.",
            HelpName = "name",
        });

        this.Options.Add(new Option<string>($"--{OverrideOptions.Content}")
        {
            Description = "The directory holding the Markdown sources.",
            HelpName = "directory",
        });

        this.Options.Add(new Option<string>($"--{OverrideOptions.Output}")
        {
            Description = "The directory the generated site is written to.",
            HelpName = "directory",
        });

        this.SetAction(async (_, cancellationToken) =>
        {
            try
            {
                IReadOnlyList<string> written = await siteBuilder
                    .BuildAsync(cancellationToken)
                    .ConfigureAwait(false);

                foreach (string page in written)
                    console.MarkupLineInterpolated($"[green]created[/] {page}");

                console.MarkupLineInterpolated($"Generated [bold]{written.Count}[/] page(s).");

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
