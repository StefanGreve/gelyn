using System.Threading.Tasks;

using Gelyn.Core.Internals;

using Microsoft.Extensions.Logging;

namespace Gelyn.Tests.Core.Internals;

/// <summary>
///     Unit tests for the <see cref="VerbosityOption"/> class.
/// </summary>
public class VerbosityOptionTests
{
    #region ReadLevel Tests

    /// <summary>
    ///     Verifies that the level is read in every form System.CommandLine accepts, behind a subcommand and
    ///     options the parse does not know.
    /// </summary>
    [Test]
    [Arguments(new[] { "build", "--title", "x", VerbosityOption.OptionName, "Debug" }, LogLevel.Debug)]
    [Arguments(new[] { "build", $"{VerbosityOption.OptionName}:trace" }, LogLevel.Trace)]
    [Arguments(new[] { "serve", "--launch-browser", $"{VerbosityOption.OptionName}=None" }, LogLevel.None)]
    public async Task ReadLevel_Test_GivenLevel_IsRead(string[] args, LogLevel expected)
    {
        // Act
        LogLevel level = VerbosityOption.ReadLevel(args);

        // Assert
        await Assert.That(level).IsEqualTo(expected);
    }

    /// <summary>
    ///     Verifies that an absent or unparsable level falls back to the default, leaving the report of the
    ///     unparsable one to the full parse.
    /// </summary>
    [Test]
    [Arguments("build")]
    [Arguments($"build {VerbosityOption.OptionName} loud")]
    [Arguments($"build {VerbosityOption.OptionName}")]
    public async Task ReadLevel_Test_AbsentOrInvalidLevel_FallsBackToTheDefault(string commandLine)
    {
        // Act
        LogLevel level = VerbosityOption.ReadLevel(commandLine.Split(' '));

        // Assert
        await Assert.That(level).IsEqualTo(VerbosityOption.DefaultLevel);
    }

    #endregion // ReadLevel Tests
}
