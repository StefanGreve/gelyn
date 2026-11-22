using System;
using System.IO;
using System.Threading.Tasks;

using Gelyn.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Gelyn.Tests.Diagnostics;

/// <summary>
///     Unit tests for the <see cref="DiagnosticLogger{T}"/> class.
/// </summary>
[Category(Categories.Internal)]
public class DiagnosticLoggerTests
{
    #region Log Tests

    /// <summary>
    ///     Verifies that the category of a record is the full name of the type the logger was created for, which
    ///     is what the documented lines show.
    /// </summary>
    [Test]
    public async Task Log_Test_Category_IsTheFullNameOfTheType()
    {
        // Arrange
        using var writer = new StringWriter();

        // Act
        using (var log = new DiagnosticLog(LogLevel.Trace, writer, colored: false, TimeProvider.System))
            log.CreateLogger<DiagnosticLoggerTests>().Log(LogLevel.Information, new EventId(7), "State", null, static (state, _) => state);

        // Assert
        await Assert.That(writer.ToString()).Contains(" info: Gelyn.Tests.Diagnostics.DiagnosticLoggerTests[7] State");
    }

    /// <summary>
    ///     Verifies that a generated log method writes its event id and its formatted message through the logger.
    /// </summary>
    [Test]
    public async Task Log_Test_GeneratedMethod_WritesTheEventIdAndTheMessage()
    {
        // Arrange
        using var writer = new StringWriter();

        // Act
        using (var log = new DiagnosticLog(LogLevel.Trace, writer, colored: false, TimeProvider.System))
            log.CreateLogger<DiagnosticLoggerTests>().LogStartupCompleted(12.345);

        // Assert
        await Assert.That(writer.ToString()).Contains(" dbug: Gelyn.Tests.Diagnostics.DiagnosticLoggerTests[1006] Started in 12.35 ms");
    }

    /// <summary>
    ///     Verifies that a record below the minimum level is neither formatted nor written.
    /// </summary>
    [Test]
    public async Task Log_Test_DisabledLevel_WritesNothing()
    {
        // Arrange
        using var writer = new StringWriter();
        bool formatted = false;

        // Act
        using (var log = new DiagnosticLog(LogLevel.Warning, writer, colored: false, TimeProvider.System))
        {
            log.CreateLogger<DiagnosticLoggerTests>().Log(LogLevel.Debug, new EventId(1), "State", null, (state, _) =>
            {
                formatted = true;
                return state;
            });
        }

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(formatted).IsFalse();
            await Assert.That(writer.ToString()).IsEmpty();
        }
    }

    #endregion // Log Tests

    #region IsEnabled Tests

    /// <summary>
    ///     Verifies that the logger answers for the level of its log.
    /// </summary>
    [Test]
    public async Task IsEnabled_Test_Level_FollowsTheLog()
    {
        // Arrange
        using var log = new DiagnosticLog(LogLevel.Warning, TextWriter.Null, colored: false, TimeProvider.System);
        ILogger<DiagnosticLoggerTests> logger = log.CreateLogger<DiagnosticLoggerTests>();

        // Act and Assert
        using (Assert.Multiple())
        {
            await Assert.That(logger.IsEnabled(LogLevel.Debug)).IsFalse();
            await Assert.That(logger.IsEnabled(LogLevel.Warning)).IsTrue();
        }
    }

    #endregion // IsEnabled Tests

    #region BeginScope Tests

    /// <summary>
    ///     Verifies that a scope is accepted and carries nothing, since the lines carry no scope information.
    /// </summary>
    [Test]
    public async Task BeginScope_Test_ReturnsNoScope()
    {
        // Arrange
        using var log = new DiagnosticLog(LogLevel.Trace, TextWriter.Null, colored: false, TimeProvider.System);
        ILogger<DiagnosticLoggerTests> logger = log.CreateLogger<DiagnosticLoggerTests>();

        // Act
        IDisposable? scope = logger.BeginScope("Scope");

        // Assert
        await Assert.That(scope).IsNull();
    }

    #endregion // BeginScope Tests
}
