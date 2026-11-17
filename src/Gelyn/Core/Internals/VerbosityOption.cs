using System.CommandLine;

using Microsoft.Extensions.Logging;

namespace Gelyn.Core.Internals;

/// <summary>
///     The option that selects how much diagnostic output the tool writes.
/// </summary>
/// <remarks>
///     Read before the host is built, because the logging providers are configured on the builder.
/// </remarks>
internal static class VerbosityOption
{
    /// <summary>
    ///     The level applied when the option is absent or cannot be parsed, chosen so that a normal build
    ///     reports nothing, because every record the build writes is <see cref="LogLevel.Debug"/>.
    /// </summary>
    internal const LogLevel DefaultLevel = LogLevel.Warning;

    /// <summary>
    ///     The option that selects the level.
    /// </summary>
    internal const string OptionName = "--verbosity";

    /// <summary>
    ///     Creates the option that selects the level, as the root command declares it.
    /// </summary>
    /// <returns>
    ///     A new option, which applies to every subcommand.
    /// </returns>
    internal static Option<LogLevel> CreateOption() => new(OptionName)
    {
        Description = "How much diagnostic output to write to standard error.",
        HelpName = "level",
        DefaultValueFactory = static _ => DefaultLevel,
        Recursive = true,
    };

    /// <summary>
    ///     Reads the level given to <see cref="OptionName"/> from the command line.
    /// </summary>
    /// <remarks>
    ///     Parsed by a root command that declares this option alone, as <see cref="ConfigurationFile.ReadPath"/>
    ///     is, because the logging providers are configured before the container that builds the full command
    ///     exists. A value that does not parse falls back to <see cref="DefaultLevel"/>, and the full parse
    ///     reports it.
    /// </remarks>
    /// <param name="args">
    ///     Command line arguments passed to the tool.
    /// </param>
    /// <returns>
    ///     The level as given, or <see cref="DefaultLevel"/>.
    /// </returns>
    internal static LogLevel ReadLevel(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        Option<LogLevel> option = CreateOption();
        var root = new RootCommand();
        root.Options.Add(option);

        ParseResult result = root.Parse(args);

        return result.GetResult(option) is { } given && given.Errors.Any()
            ? DefaultLevel
            : result.GetValue(option);
    }
}
