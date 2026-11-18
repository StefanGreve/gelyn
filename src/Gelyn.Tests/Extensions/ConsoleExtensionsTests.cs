using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Gelyn.Extensions;
using Gelyn.Tests.Fixtures;

using Microsoft.Extensions.Options;

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

    /// <summary>
    ///     Verifies that a batch on a redirected console writes a line longer than 80 columns whole.
    /// </summary>
    [Test]
    public async Task DisableWrappingWhenRedirected_Test_Batch_IsWrittenWhole()
    {
        // Arrange
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        IAnsiConsole console = CapturedConsole.Create(writer);
        string path = string.Join('/', Enumerable.Repeat("segment", 30));

        // Act
        console.DisableWrappingWhenRedirected();
        console.WriteBatch(batch => batch.MarkupLineInterpolated($"[green]created[/] {path}"));

        // Assert
        await Assert.That(writer.ToString()).IsEqualTo($"created {path}{Environment.NewLine}");
    }

    #endregion

    #region Error Tests

    /// <summary>
    ///     Verifies that each validation failure is written on a line of its own.
    /// </summary>
    [Test]
    public async Task WriteError_Test_ValidationFailures_AreWrittenOnePerLine()
    {
        // Arrange
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        IAnsiConsole console = CapturedConsole.Create(writer);
        var exception = new OptionsValidationException(Microsoft.Extensions.Options.Options.DefaultName, typeof(object), ["First failed.", "Second failed."]);

        // Act
        console.WriteError(exception);

        // Assert
        await Assert.That(writer.ToString()).IsEqualTo($"error: First failed.{Environment.NewLine}error: Second failed.{Environment.NewLine}");
    }

    /// <summary>
    ///     Verifies that nested aggregates are written by the failures they hold, a validation failure among
    ///     them split as well.
    /// </summary>
    [Test]
    public async Task WriteError_Test_NestedAggregate_IsWrittenByItsContents()
    {
        // Arrange
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        IAnsiConsole console = CapturedConsole.Create(writer);
        var validation = new OptionsValidationException(Microsoft.Extensions.Options.Options.DefaultName, typeof(object), ["First failed.", "Second failed."]);
        var exception = new AggregateException(new AggregateException(validation), new IOException("Third failed."));

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

    #region Batch Tests

    /// <summary>
    ///     Verifies that a batch writes the same text as the writes it collects would have, a line wrapped at
    ///     the console width included.
    /// </summary>
    [Test]
    public async Task WriteBatch_Test_Output_MatchesTheDirectWrites()
    {
        // Arrange
        using var direct = new StringWriter(CultureInfo.InvariantCulture);
        using var batched = new StringWriter(CultureInfo.InvariantCulture);
        IAnsiConsole directConsole = CapturedConsole.Create(direct);
        IAnsiConsole batchedConsole = CapturedConsole.Create(batched);
        string path = string.Join('/', Enumerable.Repeat("segment", 12));
        string bracketed = $"[{path}]";

        directConsole.Profile.Width = 40;
        batchedConsole.Profile.Width = 40;

        // Act
        Write(directConsole);
        batchedConsole.WriteBatch(Write);

        // Assert
        await Assert.That(batched.ToString()).IsEqualTo(direct.ToString());

        void Write(IAnsiConsole console)
        {
            console.MarkupLineInterpolated($"[green]created[/] {path}");
            console.MarkupLineInterpolated($"[yellow]removed[/] {bracketed}");
        }
    }

    /// <summary>
    ///     Verifies that the console writes to its own output again once the batch is closed, in order after
    ///     the batch.
    /// </summary>
    [Test]
    public async Task WriteBatch_Test_LaterWrites_FollowTheBatch()
    {
        // Arrange
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        IAnsiConsole console = CapturedConsole.Create(writer);

        // Act
        console.WriteBatch(static batch => batch.MarkupLine("first"));
        console.MarkupLine("second");

        // Assert
        await Assert.That(writer.ToString()).IsEqualTo($"first{Environment.NewLine}second{Environment.NewLine}");
    }

    #endregion
}
