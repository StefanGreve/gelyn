using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Gelyn.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Gelyn.Tests.Diagnostics;

/// <summary>
///     Unit tests for the <see cref="DiagnosticLog"/> class.
/// </summary>
[Category(Categories.Internal)]
public class DiagnosticLogTests
{
    private const string Category = "Gelyn.Services.SiteBuilder";

    private static readonly FakeTimeProvider Clock = new(new DateTimeOffset(2026, 11, 22, 2, 10, 11, 991, TimeSpan.Zero));

    #region IsEnabled Tests

    /// <summary>
    ///     Verifies that a level is enabled at or above the minimum, and that <see cref="LogLevel.None"/> is never
    ///     enabled, as a minimum or as a level.
    /// </summary>
    [Test]
    [Arguments(LogLevel.Trace, LogLevel.Trace, true)]
    [Arguments(LogLevel.Warning, LogLevel.Debug, false)]
    [Arguments(LogLevel.Warning, LogLevel.Warning, true)]
    [Arguments(LogLevel.Warning, LogLevel.Critical, true)]
    [Arguments(LogLevel.None, LogLevel.Critical, false)]
    [Arguments(LogLevel.Trace, LogLevel.None, false)]
    public async Task IsEnabled_Test_Level_IsComparedWithTheMinimum(LogLevel minimum, LogLevel level, bool expected)
    {
        // Arrange
        using var log = new DiagnosticLog(minimum, TextWriter.Null, colored: false, Clock);

        // Act and Assert
        await Assert.That(log.IsEnabled(level)).IsEqualTo(expected);
    }

    #endregion // IsEnabled Tests

    #region Write Tests

    /// <summary>
    ///     Verifies that a record is one line in the documented shape, with the timestamp read from the clock, the
    ///     level, the category with the event id, and the message.
    /// </summary>
    [Test]
    public async Task Write_Test_Record_IsOneLineInTheDocumentedShape()
    {
        // Arrange
        using var writer = new StringWriter();

        // Act
        using (var log = new DiagnosticLog(LogLevel.Trace, writer, colored: false, Clock))
            log.Write(LogLevel.Debug, Category, 1000, "Scanned 18 source file(s) in 0.71 ms", null);

        // Assert
        await Assert.That(writer.ToString())
            .IsEqualTo($"02:10:11.991 dbug: Gelyn.Services.SiteBuilder[1000] Scanned 18 source file(s) in 0.71 ms{Environment.NewLine}");
    }

    /// <summary>
    ///     Verifies that each level is written as its four-letter abbreviation.
    /// </summary>
    [Test]
    [Arguments(LogLevel.Trace, "trce")]
    [Arguments(LogLevel.Debug, "dbug")]
    [Arguments(LogLevel.Information, "info")]
    [Arguments(LogLevel.Warning, "warn")]
    [Arguments(LogLevel.Error, "fail")]
    [Arguments(LogLevel.Critical, "crit")]
    public async Task Write_Test_Level_IsAbbreviated(LogLevel level, string abbreviation)
    {
        // Arrange
        using var writer = new StringWriter();

        // Act
        using (var log = new DiagnosticLog(LogLevel.Trace, writer, colored: false, Clock))
            log.Write(level, Category, 1, "Message", null);

        // Assert
        await Assert.That(writer.ToString()).Contains($" {abbreviation}: {Category}[1] Message");
    }

    /// <summary>
    ///     Verifies that the exception follows the message on the same line, with its line breaks and those of the
    ///     message replaced by spaces, so that a record never spans lines.
    /// </summary>
    [Test]
    public async Task Write_Test_Exception_FollowsTheMessageOnTheSameLine()
    {
        // Arrange
        using var writer = new StringWriter();
        var exception = new InvalidOperationException($"Broken{Environment.NewLine}pipe");

        // Act
        using (var log = new DiagnosticLog(LogLevel.Trace, writer, colored: false, Clock))
            log.Write(LogLevel.Error, Category, 1104, $"Could not answer{Environment.NewLine}GET /", exception);

        // Assert
        string output = writer.ToString();

        using (Assert.Multiple())
        {
            await Assert.That(output).EndsWith($"Could not answer GET / System.InvalidOperationException: Broken pipe{Environment.NewLine}");
            await Assert.That(output.Split(Environment.NewLine)).Count().IsEqualTo(2);
        }
    }

    /// <summary>
    ///     Verifies that an empty message leaves no trailing space after the event id.
    /// </summary>
    [Test]
    public async Task Write_Test_EmptyMessage_EndsTheLineAfterTheEventId()
    {
        // Arrange
        using var writer = new StringWriter();

        // Act
        using (var log = new DiagnosticLog(LogLevel.Trace, writer, colored: false, Clock))
            log.Write(LogLevel.Information, Category, 7, string.Empty, null);

        // Assert
        await Assert.That(writer.ToString()).EndsWith($"{Category}[7]{Environment.NewLine}");
    }

    /// <summary>
    ///     Verifies that a colored log wraps the level in the escape sequences the console provider wrote, and
    ///     nothing else.
    /// </summary>
    [Test]
    public async Task Write_Test_Colored_WrapsTheLevelInEscapeSequences()
    {
        // Arrange
        using var writer = new StringWriter();

        // Act
        using (var log = new DiagnosticLog(LogLevel.Trace, writer, colored: true, Clock))
            log.Write(LogLevel.Warning, Category, 1012, "Ignored titel", null);

        // Assert
        await Assert.That(writer.ToString())
            .IsEqualTo($"02:10:11.991 \e[40m\e[1m\e[33mwarn\e[39m\e[22m\e[49m: Gelyn.Services.SiteBuilder[1012] Ignored titel{Environment.NewLine}");
    }

    /// <summary>
    ///     Verifies that a plain log writes no escape sequence at any level, which is what a redirected error
    ///     stream gets.
    /// </summary>
    [Test]
    public async Task Write_Test_Plain_WritesNoEscapeSequence()
    {
        // Arrange
        using var writer = new StringWriter();

        // Act
        using (var log = new DiagnosticLog(LogLevel.Trace, writer, colored: false, Clock))
        {
            foreach (LogLevel level in Enum.GetValues<LogLevel>().Where(static level => level != LogLevel.None))
                log.Write(level, Category, 1, "Message", null);
        }

        // Assert
        await Assert.That(writer.ToString()).DoesNotContain("\e");
    }

    /// <summary>
    ///     Verifies that the records of each thread are written in the order that thread logged them, and that
    ///     none is lost when several threads log at once.
    /// </summary>
    [Test]
    public async Task Write_Test_SeveralThreads_KeepTheOrderOfEachThread()
    {
        // Arrange
        const int threads = 8;
        const int records = 250;

        using var writer = new StringWriter();

        // Act
        using (var log = new DiagnosticLog(LogLevel.Trace, writer, colored: false, Clock))
        {
            Parallel.For(0, threads, thread =>
            {
                for (int record = 0; record < records; record++)
                    log.Write(LogLevel.Information, Category, thread, record.ToString(CultureInfo.InvariantCulture), null);
            });
        }

        // Assert
        // The event id carries the thread and the message the record, so a line reads "...[3] 17".
        string[] lines = writer.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        var sequences = lines
            .Select(static line => line[(line.LastIndexOf('[') + 1)..].Split("] "))
            .GroupBy(static parts => int.Parse(parts[0], CultureInfo.InvariantCulture), static parts => int.Parse(parts[1], CultureInfo.InvariantCulture))
            .ToList();

        using (Assert.Multiple())
        {
            await Assert.That(lines).Count().IsEqualTo(threads * records);
            await Assert.That(sequences).Count().IsEqualTo(threads);
            await Assert.That(sequences.All(static sequence => sequence.SequenceEqual(Enumerable.Range(0, records)))).IsTrue();
        }
    }

    /// <summary>
    ///     Verifies that <see cref="LogLevel.None"/> is rejected as the level of a record, since it names no level.
    /// </summary>
    [Test]
    public async Task Write_Test_None_IsRejected()
    {
        // Arrange
        using var log = new DiagnosticLog(LogLevel.Trace, TextWriter.Null, colored: false, Clock);

        // Act and Assert
        await Assert.That(() => log.Write(LogLevel.None, Category, 1, "Message", null)).Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>
    ///     Verifies that a disposed log refuses a record instead of dropping it silently.
    /// </summary>
    [Test]
    public async Task Write_Test_DisposedLog_Throws()
    {
        // Arrange
        var log = new DiagnosticLog(LogLevel.Trace, TextWriter.Null, colored: false, Clock);

        // Act
        log.Dispose();

        // Assert
        await Assert.That(() => log.Write(LogLevel.Information, Category, 1, "Message", null)).Throws<ObjectDisposedException>();
    }

    #endregion // Write Tests

    #region Dispose Tests

    /// <summary>
    ///     Verifies that every queued record is written before <see cref="DiagnosticLog.Dispose"/> returns, so that
    ///     a run exits with its last records on standard error.
    /// </summary>
    [Test]
    public async Task Dispose_Test_QueuedRecords_AreWrittenBeforeItReturns()
    {
        // Arrange
        const int records = 5000;

        using var writer = new StringWriter();
        var log = new DiagnosticLog(LogLevel.Trace, writer, colored: false, Clock);

        for (int record = 0; record < records; record++)
            log.Write(LogLevel.Information, Category, record, "Message", null);

        // Act
        log.Dispose();

        // Assert
        await Assert.That(writer.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)).Count().IsEqualTo(records);
    }

    /// <summary>
    ///     Verifies that a log that received no record writes nothing, not even a line break.
    /// </summary>
    [Test]
    public async Task Dispose_Test_NoRecord_WritesNothing()
    {
        // Arrange
        using var writer = new StringWriter();
        var log = new DiagnosticLog(LogLevel.Trace, writer, colored: false, Clock);

        // Act
        log.Dispose();

        // Assert
        await Assert.That(writer.ToString()).IsEmpty();
    }

    /// <summary>
    ///     Verifies that disposing twice is harmless.
    /// </summary>
    [Test]
    public async Task Dispose_Test_SecondCall_IsIgnored()
    {
        // Arrange
        var log = new DiagnosticLog(LogLevel.Trace, TextWriter.Null, colored: false, Clock);
        log.Dispose();

        // Act and Assert
        await Assert.That(log.Dispose).ThrowsNothing();
    }

    #endregion // Dispose Tests
}
