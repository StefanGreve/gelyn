using System.Globalization;
using System.IO;
using System.Threading.Tasks;

using Gelyn.Core.Internals.Terminal;


namespace Gelyn.Tests.Core.Internals.Terminal;

/// <summary>
///     Unit tests for the <see cref="ConsoleStream"/> class.
/// </summary>
[Category(Categories.Internal)]
public class ConsoleStreamTests
{
    #region Detection Tests

    /// <summary>
    ///     Verifies that a stream colors on a terminal that renders escape sequences alone, and that
    ///     <c>NO_COLOR</c> with any value, <c>0</c> included, and a dumb terminal switch the colors off.
    /// </summary>
    [Test]
    [Arguments(true, true, null, null, true)]
    [Arguments(true, true, null, "xterm-256color", true)]
    [Arguments(true, true, "", null, true)]
    [Arguments(false, true, null, null, false)]
    [Arguments(true, false, null, null, false)]
    [Arguments(true, true, "1", null, false)]
    [Arguments(true, true, "0", null, false)]
    [Arguments(true, true, null, "dumb", false)]
    public async Task SupportsColors_Test_Inputs_DecideTheColors(bool isTerminal, bool rendersSequences, string? noColor, string? term, bool expected)
    {
        // Act
        bool colors = ConsoleStream.SupportsColors(isTerminal, rendersSequences, noColor, term);

        // Assert
        await Assert.That(colors).IsEqualTo(expected);
    }

    #endregion // Detection Tests

    #region Paint Tests

    /// <summary>
    ///     Verifies that a stream that colors wraps the text in the codes and the reset, and that one that does
    ///     not returns the text as given.
    /// </summary>
    [Test]
    [Arguments(true, "\e[32mcreated\e[39m\e[22m\e[49m")]
    [Arguments(false, "created")]
    public async Task Paint_Test_Stream_WrapsTheTextWhenItColors(bool useColors, string expected)
    {
        // Arrange
        var console = new ConsoleStream(TextWriter.Null, isTerminal: useColors, useColors);

        // Act
        string painted = console.Paint("created", AnsiCodes.DarkGreen);

        // Assert
        await Assert.That(painted).IsEqualTo(expected);
    }

    /// <summary>
    ///     Verifies that a line reaches the writer with the line break of the platform.
    /// </summary>
    [Test]
    public async Task WriteLine_Test_Line_ReachesTheWriter()
    {
        // Arrange
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var console = new ConsoleStream(writer, isTerminal: false, useColors: false);

        // Act
        console.WriteLine("Nothing to remove at /site.");

        // Assert
        await Assert.That(writer.ToString()).IsEqualTo("Nothing to remove at /site." + System.Environment.NewLine);
    }

    #endregion // Paint Tests
}
