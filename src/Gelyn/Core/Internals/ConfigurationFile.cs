using System.CommandLine;

using Microsoft.Extensions.Configuration;

namespace Gelyn.Core.Internals;

/// <summary>
///     The configuration file the generator reads its options from.
/// </summary>
internal static class ConfigurationFile
{
    /// <summary>
    ///     The file read from the working directory when no other path is given.
    /// </summary>
    internal const string DefaultFileName = "gelyn.json";

    /// <summary>
    ///     The configuration key an explicit path is recorded under, so that every reader of the configuration
    ///     sees the path the loader was given.
    /// </summary>
    internal const string PathKey = "config";

    /// <summary>
    ///     The option that overrides <see cref="DefaultFileName"/>.
    /// </summary>
    internal const string OptionName = $"--{PathKey}";

    /// <summary>
    ///     Creates the option that overrides <see cref="DefaultFileName"/>, as the root command declares it.
    /// </summary>
    /// <returns>
    ///     A new option, which applies to every subcommand.
    /// </returns>
    internal static Option<string> CreateOption() => new(OptionName)
    {
        Description = $"Path to the configuration file. Defaults to {DefaultFileName}.",
        HelpName = "path",
        Recursive = true,
    };

    /// <summary>
    ///     Reads the path given to <see cref="OptionName"/> from the command line.
    /// </summary>
    /// <remarks>
    ///     Parsed by a root command that declares this option alone, because the file loads before the container
    ///     that builds the full command exists. An option it does not know stays a token of its own, where the
    ///     command line configuration provider pairs a valueless option such as <c>--info</c> with the path.
    /// </remarks>
    /// <param name="args">
    ///     Command line arguments passed to the tool.
    /// </param>
    /// <returns>
    ///     The path as given, or <see langword="null"/> when the option is absent.
    /// </returns>
    internal static string? Requested(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        Option<string> option = CreateOption();
        var root = new RootCommand();
        root.Options.Add(option);

        return root.Parse(args).GetValue(option);
    }

    /// <summary>
    ///     Resolves the path of the configuration file a configuration names, or of the default file.
    /// </summary>
    /// <param name="configuration">
    ///     The configuration that carries an explicit path under <see cref="PathKey"/>.
    /// </param>
    /// <param name="root">
    ///     The directory a relative path is resolved against.
    /// </param>
    /// <returns>
    ///     The absolute path of the file, whether it exists or not.
    /// </returns>
    internal static string Resolve(IConfiguration configuration, string root) =>
        PathBoundary.Resolve(root, configuration[PathKey] ?? DefaultFileName);
}
