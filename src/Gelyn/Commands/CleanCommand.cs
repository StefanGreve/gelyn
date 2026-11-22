using System.CommandLine;

using Gelyn.Cli;
using Gelyn.Model.Options;

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
    /// <param name="open">
    ///     The factory that opens the session of a parsed command line.
    /// </param>
    public CleanCommand(Func<ParseResult, Session> open)
        : base("clean", "Remove the output directory.")
    {
        foreach (Option option in OverrideOptions.DirectoryDefinitions)
            this.Options.Add(option);

        this.SetAction(parseResult =>
        {
            using Session session = open(parseResult);
            SiteOptions site = session.Options;
            string directory = session.FileSystem.Path.Combine(session.ContentRoot, site.OutputDirectory);

            if (!session.FileSystem.Directory.Exists(directory))
            {
                session.StdOut.MarkupLineInterpolated($"Nothing to remove at {directory}.");

                return ExitCodes.Success;
            }

            session.FileSystem.Directory.Delete(directory, recursive: true);
            session.StdOut.MarkupLineInterpolated($"[green]removed[/] {directory}");

            return ExitCodes.Success;
        });
    }
}
