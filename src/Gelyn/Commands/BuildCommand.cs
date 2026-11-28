using System.CommandLine;
using System.Text;

using Gelyn.Cli;
using Gelyn.Core.Internals;
using Gelyn.Core.Internals.Terminal;
using Gelyn.Model;
using Gelyn.Model.Options;
using Gelyn.Services;

namespace Gelyn.Commands;

/// <summary>
///     Generates the site.
/// </summary>
internal sealed class BuildCommand : Command
{
    private const string CleanOption = "clean";

    /// <summary>
    ///     Initializes a new instance of the <see cref="BuildCommand"/> class.
    /// </summary>
    /// <param name="open">
    ///     The factory that opens the session of a parsed command line.
    /// </param>
    public BuildCommand(Func<ParseResult, Session> open)
        : base("build", "Generate the site into the output directory.")
    {
        var clean = new Option<bool>($"--{CleanOption}")
        {
            Description = "Remove the output directory before building, dot-prefixed entries and links included.",
        };

        this.Options.Add(clean);

        foreach (Option option in OverrideOptions.Definitions)
            this.Options.Add(option);

        this.SetAction(async (parseResult, cancellationToken) =>
        {
            using Session session = open(parseResult);
            SiteBuilder siteBuilder = session.Builder;
            SiteOptions site = session.Options;

            if (parseResult.GetValue(clean))
                CleanCommand.RemoveDirectory(session, session.FileSystem.Path.Combine(session.ContentRoot, site.OutputDirectory));

            BuildReport report = await siteBuilder
                .BuildAsync(site, cancellationToken)
                .ConfigureAwait(false);

            WriteReport(session.StdOut, report);

            return ExitCodes.Success;
        });
    }

    #region Helpers

    /// <summary>
    ///     Writes the report of a build to the stream in a single write, and composes nothing for a stream that
    ///     discards its text.
    /// </summary>
    /// <param name="console">
    ///     The stream to write to.
    /// </param>
    /// <param name="report">
    ///     The report of the build.
    /// </param>
    internal static void WriteReport(ConsoleStream console, BuildReport report)
    {
        if (console.IsDiscarded) return;

        console.Writer.Write(ComposeReport(console, report));
    }

    /// <summary>
    ///     Composes the report of a build as text, colored when the stream colors.
    /// </summary>
    /// <param name="console">
    ///     The stream the text is meant for.
    /// </param>
    /// <param name="report">
    ///     The report of the build.
    /// </param>
    /// <returns>
    ///     One line per file, followed by the summary line.
    /// </returns>
    internal static string ComposeReport(ConsoleStream console, BuildReport report)
    {
        var text = new StringBuilder();

        foreach (string page in report.Pages)
            text.Append(console.Paint("created", AnsiCodes.DarkGreen)).Append(' ').AppendLine(page);

        foreach (string file in report.Generated)
            text.Append(console.Paint("created", AnsiCodes.DarkGreen)).Append(' ').AppendLine(file);

        foreach (string copy in report.Copies)
            text.Append(console.Paint("copied", AnsiCodes.DarkGreen)).Append(' ').AppendLine(copy);

        foreach (string removal in report.Removed)
            text.Append(console.Paint("removed", AnsiCodes.BrightYellow)).Append(' ').AppendLine(removal);

        return text.AppendLine(ComposeSummary(console, report)).ToString();
    }

    /// <summary>
    ///     Composes the summary line of a build, with the figures in bold when the stream colors.
    /// </summary>
    /// <param name="console">
    ///     The stream the text is meant for.
    /// </param>
    /// <param name="report">
    ///     The report of the build.
    /// </param>
    /// <returns>
    ///     The line, without its line break.
    /// </returns>
    internal static string ComposeSummary(ConsoleStream console, BuildReport report)
    {
        string count = console.Paint(report.FileCount.ToString(Tool.Culture), AnsiCodes.Bold);
        string elapsed = console.Paint(report.Elapsed.TotalMilliseconds.ToString("F2", Tool.Culture), AnsiCodes.Bold);

        return $"Processed {count} file(s) in {elapsed} ms.";
    }

    #endregion
}
