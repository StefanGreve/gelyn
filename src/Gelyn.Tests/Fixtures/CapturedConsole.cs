using System.IO;

using Gelyn.Extensions;

using Spectre.Console;

namespace Gelyn.Tests.Fixtures;

/// <summary>
///     The console a harness captures the output of.
/// </summary>
internal static class CapturedConsole
{
    /// <summary>
    ///     Creates a console whose output is plain text, free of escape sequences, whatever environment the
    ///     suite runs in.
    /// </summary>
    /// <remarks>
    ///     Disabling the default enrichers is the reason this is not a bare <c>AnsiConsole.Create</c> call.
    ///     They run after the settings and raise whatever capabilities they detect, so <c>GitHubEnricher</c>
    ///     turns ANSI back on from <c>GITHUB_ACTIONS</c> and a decoration such as <c>[bold]</c> reaches the
    ///     capture as an escape sequence. <see cref="ColorSystemSupport.NoColors"/> drops the colors either
    ///     way, which is what once left a decoration as the only leak and only on CI. The width is pinned as the
    ///     tool pins it for redirected output, because Spectre reads the width of the host console whatever the
    ///     writer is, which would wrap a captured line at one width locally and at another on CI.
    /// </remarks>
    /// <param name="writer">
    ///     Receives everything the console writes.
    /// </param>
    /// <returns>
    ///     The new console.
    /// </returns>
    public static IAnsiConsole Create(TextWriter writer)
    {
        IAnsiConsole console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Enrichment = new ProfileEnrichment { UseDefaultEnrichers = false },
            Out = new AnsiConsoleOutput(writer),
        });

        console.DisableWrappingWhenRedirected();

        return console;
    }
}
