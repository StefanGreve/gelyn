using System.CommandLine;

namespace Gelyn.Cli;

/// <summary>
///     Provides the command line option and the name of the quiet setting.
/// </summary>
internal static class QuietOption
{
    /// <summary>
    ///     The name of the option on the command line.
    /// </summary>
    internal const string OptionName = "--quiet";

    /// <summary>
    ///     Creates the <c>--quiet</c> option, which the root command declares and every subcommand accepts.
    /// </summary>
    /// <returns>
    ///     A new instance of the option.
    /// </returns>
    internal static Option<bool> CreateOption() => new(OptionName)
    {
        Description = $"Write nothing to standard output. Standard error still follows {VerbosityOption.OptionName}.",
        Recursive = true,
    };
}
