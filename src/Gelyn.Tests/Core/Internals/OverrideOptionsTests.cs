using System.CommandLine;
using System.Threading.Tasks;

using Gelyn.Core.Internals;

namespace Gelyn.Tests.Core.Internals;

/// <summary>
///     Unit tests for the <see cref="OverrideOptions"/> class.
/// </summary>
public class OverrideOptionsTests
{
    #region Switch Tests

    /// <summary>
    ///     Verifies that each switch accepts an explicit value in either direction.
    /// </summary>
    [Test]
    [Arguments(OverrideOptions.Feed, "true")]
    [Arguments(OverrideOptions.Feed, "false")]
    [Arguments(OverrideOptions.Sitemap, "true")]
    [Arguments(OverrideOptions.Sitemap, "false")]
    public async Task Switch_Test_ExplicitValue_Parses(string name, string value)
    {
        // Arrange
        RootCommand command = Command();

        // Act
        ParseResult result = command.Parse([$"--{name}", value, "--theme", "modern"]);

        // Assert
        await Assert.That(result.Errors).IsEmpty();
    }

    /// <summary>
    ///     Verifies that a switch without a value is rejected at parse time, whether it ends the command line
    ///     or another option follows it.
    /// </summary>
    /// <remarks>
    ///     The configuration provider that promotes the value takes the token after a bare switch as its
    ///     value, so a bare <c>--feed</c> ahead of <c>--theme modern</c> would promote "--theme" as the feed
    ///     setting and drop the theme.
    /// </remarks>
    [Test]
    [Arguments(OverrideOptions.Feed, "")]
    [Arguments(OverrideOptions.Feed, " --theme modern")]
    [Arguments(OverrideOptions.Sitemap, "")]
    [Arguments(OverrideOptions.Sitemap, " --theme modern")]
    public async Task Switch_Test_BareSwitch_IsRejected(string name, string rest)
    {
        // Arrange
        RootCommand command = Command();

        // Act
        ParseResult result = command.Parse($"--{name}{rest}".Split(' '));

        // Assert
        await Assert.That(result.Errors).IsNotEmpty();
    }

    #endregion // Switch Tests

    #region Helpers

    private static RootCommand Command()
    {
        var command = new RootCommand();

        foreach (Option option in OverrideOptions.Definitions)
            command.Options.Add(option);

        return command;
    }

    #endregion
}
