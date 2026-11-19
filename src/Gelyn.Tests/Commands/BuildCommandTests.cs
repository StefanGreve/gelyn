using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

using Gelyn.Commands;
using Gelyn.Extensions;
using Gelyn.Model;
using Gelyn.Tests.Stubs;

using Spectre.Console;

namespace Gelyn.Tests.Commands;

/// <summary>
///     Unit tests for the <see cref="BuildCommand"/> class.
/// </summary>
[Category(Categories.Internal)]
public class BuildCommandTests
{
    private static readonly BuildReport Report = new()
    {
        Pages = ["index.html", "[draft]/notes.html"],
        Generated = ["feed.xml"],
        Copies = ["images/logo.png"],
        Removed = ["old.html"],
        Elapsed = TimeSpan.FromMilliseconds(12.345),
    };

    #region Report Tests

    /// <summary>
    ///     Verifies that the composed report is byte for byte what the console renders line by line, with and
    ///     without escape sequences and in every color system.
    /// </summary>
    [Test]
    [Arguments(false, ColorSystemSupport.NoColors)]
    [Arguments(true, ColorSystemSupport.NoColors)]
    [Arguments(true, ColorSystemSupport.Legacy)]
    [Arguments(true, ColorSystemSupport.Standard)]
    [Arguments(true, ColorSystemSupport.EightBit)]
    [Arguments(true, ColorSystemSupport.TrueColor)]
    public async Task ComposeReport_Test_Output_MatchesTheConsoleRendering(bool ansi, ColorSystemSupport colorSystem)
    {
        // Arrange
        using var rendered = new StringWriter(CultureInfo.InvariantCulture);
        IAnsiConsole console = CreateConsole(new AnsiConsoleOutput(rendered), ansi, colorSystem);
        var capabilities = new AnsiCapabilities
        {
            Ansi = console.Profile.Capabilities.Ansi,
            ColorSystem = console.Profile.Capabilities.ColorSystem
        };

        // Act
        BuildCommand.PrintReport(console, Report);
        string composed = BuildCommand.ComposeReport(Report, capabilities);

        // Assert
        await Assert.That(composed).IsEqualTo(rendered.ToString());
        await Assert.That(composed.Contains('\e', StringComparison.Ordinal)).IsEqualTo(ansi);
    }

    /// <summary>
    ///     Verifies that an emoji shortcode in a path is written as given, where the console's markup would
    ///     replace it with the emoji.
    /// </summary>
    [Test]
    public async Task ComposeReport_Test_EmojiShortcode_IsWrittenAsGiven()
    {
        // Arrange
        BuildReport report = Report with { Pages = [":rocket:.html"] };
        var capabilities = new AnsiCapabilities
        {
            Ansi = false,
            ColorSystem = ColorSystem.NoColors
        };

        // Act
        string composed = BuildCommand.ComposeReport(report, capabilities);

        // Assert
        await Assert.That(composed).StartsWith($"created :rocket:.html{Environment.NewLine}");
    }

    /// <summary>
    ///     Verifies that an output that is not a terminal receives the whole report in a single write.
    /// </summary>
    [Test]
    public async Task WriteReport_Test_RedirectedOutput_ReceivesOneWrite()
    {
        // Arrange
        using var writer = new CountingWriter();
        IAnsiConsole console = CreateConsole(new AnsiConsoleOutputStub(writer, isTerminal: false), ansi: false, ColorSystemSupport.NoColors);

        // Act
        BuildCommand.WriteReport(console, Report);

        // Assert
        await Assert.That(writer.Writes).IsEqualTo(1);
    }

    /// <summary>
    ///     Verifies that a terminal without escape sequences receives the report through the console, write by
    ///     write, so that the console API can color the text between writes.
    /// </summary>
    [Test]
    public async Task WriteReport_Test_TerminalWithoutAnsi_IsWrittenThroughTheConsole()
    {
        // Arrange
        using var writer = new CountingWriter();
        IAnsiConsole console = CreateConsole(new AnsiConsoleOutputStub(writer, isTerminal: true), ansi: false, ColorSystemSupport.NoColors);

        // Act
        BuildCommand.WriteReport(console, Report);

        // Assert
        await Assert.That(writer.Writes).IsGreaterThan(1);
    }

    #endregion

    #region Helpers

    private static IAnsiConsole CreateConsole(IAnsiConsoleOutput output, bool ansi, ColorSystemSupport colorSystem)
    {
        IAnsiConsole console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = ansi ? AnsiSupport.Yes : AnsiSupport.No,
            ColorSystem = colorSystem,
            Enrichment = new ProfileEnrichment { UseDefaultEnrichers = false },
            Out = output,
        });

        console.DisableWrappingWhenRedirected();

        return console;
    }

    private sealed class CountingWriter() : StringWriter(CultureInfo.InvariantCulture)
    {
        public int Writes { get; private set; }

        public override void Write(char value)
        {
            this.Writes++;
            base.Write(value);
        }

        public override void Write(char[] buffer, int index, int count)
        {
            this.Writes++;
            base.Write(buffer, index, count);
        }

        public override void Write(ReadOnlySpan<char> buffer)
        {
            this.Writes++;
            base.Write(buffer);
        }

        public override void Write(string? value)
        {
            this.Writes++;
            base.Write(value);
        }
    }

    #endregion
}
