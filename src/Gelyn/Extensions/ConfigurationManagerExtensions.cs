using Gelyn.Core.Internals;
using Gelyn.Model;
using Gelyn.Model.Options;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.FileProviders;

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
    ///     A load failure is returned instead of thrown, so that <c>--help</c> and <c>--version</c> still run. The
    ///     file is not watched, because the watch costs most of the startup and <c>serve</c> runs its own watcher.
    /// </remarks>
    /// <param name="configuration">
    ///     The configuration to add the sources to.
    /// </param>
    /// <param name="args">
    ///     Command line arguments passed to the tool.
    /// </param>
    /// <returns>
    ///     What the first load of the file found, including the failure it held back.
    /// </returns>
    public static ConfigurationFileStatus AddSiteConfiguration(this ConfigurationManager configuration, string[] args)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string? requested = configuration[ConfigurationFile.PathKey];
        IReadOnlyDictionary<string, string?> overrides = configuration.ReadOverrides();

        JsonConfigurationSource? file = null;
        Exception? failure = null;

        configuration.AddJsonFile(source =>
        {
            source.Path = requested ?? ConfigurationFile.DefaultFileName;
            source.Optional = requested is null;
            source.ReloadOnChange = false;
            source.ResolveFileProvider();
            source.OnLoadException = context =>
            {
                failure = context.Exception;
                context.Ignore = true;
            };
            file = source;
        });

        // Only the first load is deferred, so a reload under serve still throws into the watcher.
        if (file is not null) file.OnLoadException = null;

        configuration
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .AddInMemoryCollection(overrides);

        return new ConfigurationFileStatus
        {
            Exists = file is { FileProvider: IFileProvider provider, Path: string path } && provider.GetFileInfo(path).Exists,
            Failure = failure,
        };
    }

    #region Helpers

    private static Dictionary<string, string?> ReadOverrides(this ConfigurationManager configuration)
    {
        (string Option, string Key)[] promotions =
        [
            (OverrideOptions.Title, nameof(SiteOptions.Title)),
            (OverrideOptions.BaseUrl, nameof(SiteOptions.BaseUrl)),
            (OverrideOptions.Theme, nameof(SiteOptions.Theme)),
            (OverrideOptions.Feed, nameof(SiteOptions.EnableFeed)),
            (OverrideOptions.Sitemap, nameof(SiteOptions.EnableSitemap)),
            (OverrideOptions.Content, nameof(SiteOptions.ContentDirectory)),
            (OverrideOptions.Output, nameof(SiteOptions.OutputDirectory)),
            (OverrideOptions.Port, nameof(SiteOptions.Port)),
        ];

        Dictionary<string, string?> overrides = [];

        foreach ((string option, string key) in promotions)
        {
            if (configuration[option] is string value) overrides[key] = value;
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
