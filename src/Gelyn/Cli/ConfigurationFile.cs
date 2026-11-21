using System.CommandLine;

using Gelyn.Core.Internals;

namespace Gelyn.Cli;

/// <summary>
///     Provides the default name, the command line option and the path resolution of the configuration file.
/// </summary>
internal static class ConfigurationFile
{
    /// <summary>
    ///     The file name resolved against the content root when <see cref="OptionName"/> is absent.
    /// </summary>
    internal const string DefaultFileName = "gelyn.json";

    /// <summary>
    ///     The name of the option on the command line.
    /// </summary>
    internal const string OptionName = "--config";

    /// <summary>
    ///     Creates the <c>--config</c> option, which the root command declares and every subcommand accepts.
    /// </summary>
    /// <returns>
    ///     A new instance of the option.
    /// </returns>
    internal static Option<string> CreateOption() => new(OptionName)
    {
        Description = $"Path to the configuration file. Defaults to {DefaultFileName}.",
        HelpName = "path",
        Recursive = true,
    };

    /// <summary>
    ///     Resolves the requested path against the content root, or <see cref="DefaultFileName"/> in the content
    ///     root when no path was requested.
    /// </summary>
    /// <param name="contentRoot">
    ///     The directory a relative path is resolved against.
    /// </param>
    /// <param name="requested">
    ///     The path as given to <see cref="OptionName"/>, or <see langword="null"/> when the option is absent.
    /// </param>
    /// <returns>
    ///     The absolute path of the file, whether it exists or not.
    /// </returns>
    internal static string Resolve(string contentRoot, string? requested) =>
        PathBoundary.Resolve(contentRoot, requested ?? DefaultFileName);
}
