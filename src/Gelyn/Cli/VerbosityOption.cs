using System.CommandLine;

using Microsoft.Extensions.Logging;

namespace Gelyn.Cli;

/// <summary>
///     Provides the default level, the command line option and the name of the verbosity setting.
/// </summary>
internal static class VerbosityOption
{
    /// <summary>
    ///     The level applied when the option is absent.
    /// </summary>
    internal const LogLevel DefaultLevel = LogLevel.Warning;

    /// <summary>
    ///     The name of the option on the command line.
    /// </summary>
    internal const string OptionName = "--verbosity";

    /// <summary>
    ///     Creates the <c>--verbosity</c> option, which the root command declares and every subcommand accepts.
    /// </summary>
    /// <returns>
    ///     A new instance of the option.
    /// </returns>
    internal static Option<LogLevel> CreateOption() => new(OptionName) // Listed in the description, as the help name sets the column width
    {
        Description = $"How much diagnostic output to write to standard error: {string.Join(", ", Enum.GetNames<LogLevel>())}.",
        HelpName = "level",
        DefaultValueFactory = static _ => DefaultLevel,
        Recursive = true,
    };
}
