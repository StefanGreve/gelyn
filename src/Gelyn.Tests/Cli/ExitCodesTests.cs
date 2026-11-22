using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Gelyn.Cli;
using Gelyn.Exceptions;

namespace Gelyn.Tests.Cli;

/// <summary>
///     Unit tests for the <see cref="ExitCodes"/> class.
/// </summary>
[Category(Categories.Internal)]
public class ExitCodesTests
{
    #region For Tests

    /// <summary>
    ///     Verifies that a rejected configuration reports the configuration error.
    /// </summary>
    [Test]
    public async Task For_Test_RejectedConfiguration_ReportsTheConfigurationError()
    {
        // Arrange
        var exception = new ConfigurationException(["Title must not be empty.", "BaseUrl must be an absolute http or https URL."]);

        // Act
        int code = ExitCodes.For(exception);

        // Assert
        await Assert.That(code).IsEqualTo(ExitCodes.ConfigurationError);
    }

    /// <summary>
    ///     Verifies that any other failure reports the plain error, a file that disappeared during the build
    ///     and an unwrapped failure of the configuration binder included.
    /// </summary>
    [Test]
    [MethodDataSource(nameof(OtherFailures))]
    public async Task For_Test_OtherFailure_ReportsTheError(Exception exception)
    {
        // Act
        int code = ExitCodes.For(exception);

        // Assert
        await Assert.That(code).IsEqualTo(ExitCodes.Error);
    }

    #endregion // For Tests

    #region Helpers

    /// <summary>
    ///     Failures of a build, and the exceptions the loader wraps before they reach the mapping.
    /// </summary>
    public static IEnumerable<Func<Exception>> OtherFailures()
    {
        yield return () => new FileNotFoundException("Could not find file 'content/about.md'.");
        yield return () => new IOException("The process cannot access the file because it is being used by another process.");
        yield return () => new InvalidDataException("Failed to load configuration from file 'gelyn.json'.");
        yield return () => new InvalidOperationException("Failed to convert configuration value 'yes' at 'EnableFeed' to type 'System.Boolean'.", new FormatException());
        yield return () => new UnauthorizedAccessException("Access to the path is denied.");
    }

    #endregion
}
