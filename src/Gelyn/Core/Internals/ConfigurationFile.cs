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
    ///     The configuration key an explicit path arrives under, which is how the command line provider
    ///     registered by the host exposes <see cref="OptionName"/>.
    /// </summary>
    internal const string PathKey = "config";

    /// <summary>
    ///     The option that overrides <see cref="DefaultFileName"/>.
    /// </summary>
    internal const string OptionName = $"--{PathKey}";

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
