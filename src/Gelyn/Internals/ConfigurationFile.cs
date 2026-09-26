namespace Gelyn.Internals;

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
}
