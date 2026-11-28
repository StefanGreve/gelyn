using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

using Gelyn.Core.Internals.Terminal;
using Gelyn.Exceptions;
using Gelyn.Extensions;
using Gelyn.Tests.Fixtures;

namespace Gelyn.Tests.Extensions;

/// <summary>
///     Unit tests for the <see cref="ConsoleExtensions"/> class.
/// </summary>
[Category(Categories.Internal)]
public class ConsoleExtensionsTests
{
    #region Error Tests

    /// <summary>
    ///     Verifies that each failure of a rejected configuration is written on a line of its own.
    /// </summary>
    [Test]
    public async Task WriteError_Test_ConfigurationFailures_AreWrittenOnePerLine()
    {
        // Arrange
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        ConsoleStream console = CapturedConsole.Create(writer);
        var exception = new ConfigurationException(["First failed.", "Second failed."]);

        // Act
        console.WriteError(exception);

        // Assert
        await Assert.That(writer.ToString()).IsEqualTo($"error: First failed.{Environment.NewLine}error: Second failed.{Environment.NewLine}");
    }

    /// <summary>
    ///     Verifies that any other exception is written by its message.
    /// </summary>
    [Test]
    public async Task WriteError_Test_OtherException_IsWrittenByItsMessage()
    {
        // Arrange
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        ConsoleStream console = CapturedConsole.Create(writer);

        // Act
        console.WriteError(new IOException("Access [denied]."));

        // Assert
        await Assert.That(writer.ToString()).IsEqualTo($"error: Access [denied].{Environment.NewLine}");
    }

    /// <summary>
    ///     Verifies that the word before a failure is colored on a stream that colors.
    /// </summary>
    [Test]
    public async Task WriteError_Test_ColoringStream_ColorsTheWord()
    {
        // Arrange
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var console = new ConsoleStream(writer, isTerminal: true, useColors: true);

        // Act
        console.WriteError(new IOException("Access denied."));

        // Assert
        await Assert.That(writer.ToString()).IsEqualTo($"{AnsiCodes.BrightRed}error:{AnsiCodes.Reset} Access denied.{Environment.NewLine}");
    }

    #endregion
}
