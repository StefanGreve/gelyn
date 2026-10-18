using System;
using System.Collections.Generic;

using Gelyn.Core.Internals;
using Gelyn.Model.Options;

using Microsoft.Extensions.Configuration;

namespace Gelyn.Extensions;

/// <summary>
///     Configuration sources the composition root in <see cref="Program"/> layers on top of the environment
///     variables and command line it seeds the host configuration with.
/// </summary>
internal static class ConfigurationManagerExtensions
{
    /// <summary>
    ///     Adds the tool's configuration file and re-applies the sources that outrank it.
    /// </summary>
    /// <remarks>
    ///     A path supplied through <c>--config</c> must exist, whereas the conventional
    ///     <c>gelyn.json</c> is optional, because the defaults on <see cref="SiteOptions"/> are usable on
    ///     their own. The environment variables and the command line are added again so that they keep
    ///     overriding the file, which is added after them and would otherwise win.
    ///
    ///     The file is not watched here, because establishing the watch costs around 38 ms of a 41 ms startup
    ///     on macOS and every command but <c>serve</c> reads it once. <c>serve</c> does outlive that read and
    ///     runs a watcher of its own in <see cref="Gelyn.Services.SiteWatcher"/>, which reloads the
    ///     configuration and then regenerates the site, where a reload alone would update what
    ///     <c>IOptionsMonitor</c> returns and nothing else.
    /// </remarks>
    /// <param name="configuration">
    ///     The configuration to add the sources to.
    /// </param>
    /// <param name="args">
    ///     Command line arguments passed to the tool.
    /// </param>
    /// <returns>
    ///     The same <see cref="ConfigurationManager"/> instance, so that calls can be chained.
    /// </returns>
    public static ConfigurationManager AddSiteConfiguration(this ConfigurationManager configuration, string[] args)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string? requested = configuration[ConfigurationFile.PathKey];
        IReadOnlyDictionary<string, string?> overrides = configuration.ReadOverrides();

        configuration.AddJsonFile(
            requested ?? ConfigurationFile.DefaultFileName,
            optional: requested is null,
            reloadOnChange: false);

        configuration
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .AddInMemoryCollection(overrides);

        return configuration;
    }

    #region Helpers

    private static Dictionary<string, string?> ReadOverrides(this ConfigurationManager configuration)
    {
        (string Option, string Key)[] promotions =
        [
            (OverrideOptions.Title, nameof(SiteOptions.Title)),
            (OverrideOptions.BaseUrl, nameof(SiteOptions.BaseUrl)),
            (OverrideOptions.Content, nameof(SiteOptions.ContentDirectory)),
            (OverrideOptions.Output, nameof(SiteOptions.OutputDirectory)),
        ];

        Dictionary<string, string?> overrides = [];

        foreach ((string option, string key) in promotions)
        {
            if (configuration[option] is string value)
                overrides[key] = value;
        }

        if (configuration[OverrideOptions.Exclude] is string patterns)
        {
            string[] split = patterns.Split(OverrideOptions.ExcludeSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            for (int index = 0; index < split.Length; index++)
            {
                string key = $"{nameof(SiteOptions.ExcludePatterns)}:{OverrideOptions.ExcludeOffset + index}";
                overrides[key] = split[index];
            }
        }

        return overrides;
    }

    #endregion
}
