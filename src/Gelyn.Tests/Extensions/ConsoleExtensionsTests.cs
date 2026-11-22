using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Gelyn.Core.Internals;
using Gelyn.Extensions;
using Gelyn.Tests.Fixtures;

using Spectre.Console;

namespace Gelyn.Tests.Extensions;

/// <summary>
///     Unit tests for the <see cref="ConsoleExtensions"/> class.
/// </summary>
[Category(Categories.Internal)]
public class ConsoleExtensionsTests
{
    #region Wrapping Tests

    /// <summary>
    ///     Verifies that a console whose output is not a terminal writes a line longer than 80 columns whole.
    /// </summary>
    [Test]
    public async Task DisableWrappingWhenRedirected_Test_LongLine_IsWrittenWhole()
    {
        // Arrange
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        IAnsiConsole console = CapturedConsole.Create(writer);
        string path = string.Join('/', Enumerable.Repeat("segment", 30));

        // Act
        console.DisableWrappingWhenRedirected();
        console.MarkupLineInterpolated($"[green]created[/] {path}");

        // Assert
        await Assert.That(writer.ToString()).IsEqualTo($"created {path}{Environment.NewLine}");
    }

    #endregion

    #region Error Tests

    /// <summary>
    ///     Verifies that each failure of a rejected configuration is written on a line of its own.
    /// </summary>
    [Test]
    public async Task WriteError_Test_ConfigurationFailures_AreWrittenOnePerLine()
    {
        // Arrange
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        IAnsiConsole console = CapturedConsole.Create(writer);
        var exception = new ConfigurationException(["First failed.", "Second failed."]);

        // Act
        console.WriteError(exception);

        // Assert
        await Assert.That(writer.ToString()).IsEqualTo($"error: First failed.{Environment.NewLine}error: Second failed.{Environment.NewLine}");
    }

    /// <summary>
    ///     Verifies that nested aggregates are written by the failures they hold, a rejected configuration among
    ///     them split as well.
    /// </summary>
    [Test]
    public async Task WriteError_Test_NestedAggregate_IsWrittenByItsContents()
    {
        // Arrange
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        IAnsiConsole console = CapturedConsole.Create(writer);
        var configuration = new ConfigurationException(["First failed.", "Second failed."]);
        var exception = new AggregateException(new AggregateException(configuration), new IOException("Third failed."));

        // Act
        console.WriteError(exception);

        // Assert
        await Assert.That(writer.ToString()).IsEqualTo($"error: First failed.{Environment.NewLine}error: Second failed.{Environment.NewLine}error: Third failed.{Environment.NewLine}");
    }

    /// <summary>
    ///     Verifies that any other exception is written by its message.
    /// </summary>
    [Test]
    public async Task WriteError_Test_OtherException_IsWrittenByItsMessage()
    {
        // Arrange
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        IAnsiConsole console = CapturedConsole.Create(writer);

        // Act
        console.WriteError(new IOException("Access [denied]."));

        // Assert
        await Assert.That(writer.ToString()).IsEqualTo($"error: Access [denied].{Environment.NewLine}");
    }

    #endregion
}
