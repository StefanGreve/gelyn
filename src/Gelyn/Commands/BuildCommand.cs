using System.CommandLine;

using Gelyn.Abstractions;
using Gelyn.Cli;
using Gelyn.Core.Internals;
using Gelyn.Extensions;
using Gelyn.Model;

using Microsoft.Extensions.DependencyInjection;

using Spectre.Console;

namespace Gelyn.Commands;

/// <summary>
///     Generates the site.
/// </summary>
internal sealed class BuildCommand : Command
{
    private static readonly Style CreatedStyle = new(foreground: Color.Green);
    private static readonly Style RemovedStyle = new(foreground: Color.Yellow);
    private static readonly Style FigureStyle = new(decoration: Decoration.Bold);

    /// <summary>
    ///     Initializes a new instance of the <see cref="BuildCommand"/> class.
    /// </summary>
    /// <param name="open">
    ///     The factory that opens the session of a parsed command line.
    /// </param>
    public BuildCommand(Func<ParseResult, Session> open)
        : base("build", "Generate the site into the output directory.")
    {
        foreach (Option option in OverrideOptions.Definitions)
            this.Options.Add(option);

        this.SetAction(async (parseResult, cancellationToken) =>
        {
            using Session session = open(parseResult);
            var siteBuilder = session.Services.GetRequiredService<SiteBuilderContract>();

            try
            {
                BuildReport report = await siteBuilder
                    .BuildAsync(cancellationToken)
                    .ConfigureAwait(false);

                WriteReport(session.StdOut, report);

                return ExitCodes.Success;
            }
            catch (IOException exception) when (exception is not FileNotFoundException)
            {
                session.StdErr.WriteError(exception);
                return ExitCodes.Error;
            }
        });
    }

    #region Helpers

    /// <summary>
    ///     Writes the report of a build to the console in a single write.
    /// </summary>
    /// <remarks>
    ///     A terminal without escape sequences colors its text through the console API between writes, so it
    ///     receives the report line by line instead.
    /// </remarks>
    /// <param name="console">
    ///     The console to write to.
    /// </param>
    /// <param name="report">
    ///     The report of the build.
    /// </param>
    internal static void WriteReport(IAnsiConsole console, BuildReport report)
    {
        Profile profile = console.Profile;

        if (profile.Out.IsTerminal && !profile.Capabilities.Ansi)
        {
            PrintReport(console, report);
            return;
        }

        var capabilities = new AnsiCapabilities
        {
            Ansi = profile.Capabilities.Ansi,
            ColorSystem = profile.Capabilities.ColorSystem
        };

        profile.Out.Writer.Write(ComposeReport(report, capabilities));
    }

    /// <summary>
    ///     Composes the report of a build as text, with the escape sequences the capabilities allow.
    /// </summary>
    /// <remarks>
    ///     Paths are written as given, without markup, emoji or line wrapping.
    /// </remarks>
    /// <param name="report">
    ///     The report of the build.
    /// </param>
    /// <param name="capabilities">
    ///     The capabilities of the output the text is meant for.
    /// </param>
    /// <returns>
    ///     One line per file, followed by the summary line.
    /// </returns>
    internal static string ComposeReport(BuildReport report, AnsiCapabilities capabilities)
    {
        using var text = new StringWriter(Tool.Culture);
        var writer = new AnsiWriter(text, capabilities);

        foreach (string page in report.Pages)
            writer.Write("created", CreatedStyle, null).Write(" ").WriteLine(page);

        foreach (string file in report.Generated)
            writer.Write("created", CreatedStyle, null).Write(" ").WriteLine(file);

        foreach (string copy in report.Copies)
            writer.Write("copied", CreatedStyle, null).Write(" ").WriteLine(copy);

        foreach (string removal in report.Removed)
            writer.Write("removed", RemovedStyle, null).Write(" ").WriteLine(removal);

        string elapsed = report.Elapsed.TotalMilliseconds.ToString("F2", Tool.Culture);

        writer.Write("Processed ")
            .Write(report.FileCount.ToString(Tool.Culture), FigureStyle, null)
            .Write(" file(s) in ")
            .Write(elapsed, FigureStyle, null)
            .WriteLine(" ms.");

        return text.ToString();
    }

    /// <summary>
    ///     Renders the report of a build through the console, one markup line per file.
    /// </summary>
    /// <param name="console">
    ///     The console to render to.
    /// </param>
    /// <param name="report">
    ///     The report of the build.
    /// </param>
    internal static void PrintReport(IAnsiConsole console, BuildReport report)
    {
        foreach (string page in report.Pages)
            console.MarkupLineInterpolated($"[green]created[/] {page}");

        foreach (string file in report.Generated)
            console.MarkupLineInterpolated($"[green]created[/] {file}");

        foreach (string copy in report.Copies)
            console.MarkupLineInterpolated($"[green]copied[/] {copy}");

        foreach (string removal in report.Removed)
            console.MarkupLineInterpolated($"[yellow]removed[/] {removal}");

        string elapsed = report.Elapsed.TotalMilliseconds.ToString("F2", Tool.Culture);
        console.MarkupLineInterpolated(Tool.Culture, $"Processed [bold]{report.FileCount}[/] file(s) in [bold]{elapsed}[/] ms.");
    }

    #endregion
}
