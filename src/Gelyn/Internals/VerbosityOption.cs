using Microsoft.Extensions.Logging;

namespace Gelyn.Internals;

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
    ///     The configuration key the level arrives under, which is how the command line provider exposes
    ///     <see cref="OptionName"/>.
    /// </summary>
    internal const string LevelKey = "verbosity";

    /// <summary>
    ///     The option that selects the level.
    /// </summary>
    internal const string OptionName = $"--{LevelKey}";
}
