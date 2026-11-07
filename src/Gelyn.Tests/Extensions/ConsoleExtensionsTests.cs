using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Gelyn.Extensions;
using Gelyn.Tests.Fixtures;

using Spectre.Console;

namespace Gelyn.Tests.Extensions;

/// <summary>
///     Unit tests for the <see cref="ConsoleExtensions"/> class.
/// </summary>
public class ConsoleExtensionsTests
{
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
