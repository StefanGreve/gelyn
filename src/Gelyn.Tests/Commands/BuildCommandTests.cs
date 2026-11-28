using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

using Gelyn.Commands;
using Gelyn.Core.Internals.Terminal;
using Gelyn.Model;

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
        Elapsed = TimeSpan.FromMilliseconds(12.5),
    };

    #region Report Tests

    /// <summary>
    ///     Verifies that the report names each file on a line of its own with the paths as given, and that the
    ///     words and the figures are colored on a stream that colors and plain on one that does not.
    /// </summary>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ComposeReport_Test_Stream_ColorsTheWordsWhenItColors(bool useColors)
    {
        // Arrange
        var console = new ConsoleStream(TextWriter.Null, isTerminal: useColors, useColors);
        string count = Report.FileCount.ToString(CultureInfo.InvariantCulture);
        string expected = string.Join(Environment.NewLine,
        [
            $"{Paint("created", AnsiCodes.DarkGreen, useColors)} index.html",
            $"{Paint("created", AnsiCodes.DarkGreen, useColors)} [draft]/notes.html",
            $"{Paint("created", AnsiCodes.DarkGreen, useColors)} feed.xml",
            $"{Paint("copied", AnsiCodes.DarkGreen, useColors)} images/logo.png",
            $"{Paint("removed", AnsiCodes.BrightYellow, useColors)} old.html",
            $"Processed {Paint(count, AnsiCodes.Bold, useColors)} file(s) in {Paint("12.50", AnsiCodes.Bold, useColors)} ms.",
            "",
        ]);

        // Act
        string composed = BuildCommand.ComposeReport(console, Report);

        // Assert
        await Assert.That(composed).IsEqualTo(expected);
    }

    /// <summary>
    ///     Verifies that the stream receives the whole report in a single write.
    /// </summary>
    [Test]
    public async Task WriteReport_Test_Stream_ReceivesOneWrite()
    {
        // Arrange
        using var writer = new CountingWriter();
        var console = new ConsoleStream(writer, isTerminal: false, useColors: false);

        // Act
        BuildCommand.WriteReport(console, Report);

        // Assert
        await Assert.That(writer.Writes).IsEqualTo(1);
    }

    #endregion

    #region Helpers

    private static string Paint(string text, string codes, bool useColors) =>
        useColors ? codes + text + AnsiCodes.Reset : text;

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
