using System.CommandLine;

using Gelyn.Cli;
using Gelyn.Core.Internals;
using Gelyn.Model.Options;

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

            if (!RemoveDirectory(session, directory))
                session.StdOut.WriteLine($"Nothing to remove at {directory}.");

            return ExitCodes.Success;
        });
    }

    #region Helpers

    /// <summary>
    ///     Removes a directory and everything in it, and reports the removal on standard output.
    /// </summary>
    /// <param name="session">
    ///     The session whose file system and standard output to use.
    /// </param>
    /// <param name="directory">
    ///     The full path of the directory to remove.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> if the directory existed and was removed; otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool RemoveDirectory(Session session, string directory)
    {
        if (!session.FileSystem.Directory.Exists(directory))
            return false;

        session.FileSystem.Directory.Delete(directory, recursive: true);
        session.StdOut.WriteLine($"{session.StdOut.Paint("removed", AnsiCodes.DarkGreen)} {directory}");

        return true;
    }

    #endregion
}
