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
    /// <param name="requested">
    ///     The path given to <see cref="ConfigurationFile.OptionName"/>, as <see cref="ConfigurationFile.Requested"/>
    ///     reads it from the parsed command line, or <see langword="null"/> for the default file.
    /// </param>
    /// <returns>
    ///     What the first load of the file found, including the failure it held back.
    /// </returns>
    public static ConfigurationFileStatus AddSiteConfiguration(this ConfigurationManager configuration, string[] args, string? requested)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        Dictionary<string, string?> overrides = configuration.ReadOverrides();

        // Outranks the command line provider's pairing, so a later reader such as the serve watcher sees this path.
        overrides[ConfigurationFile.PathKey] = requested;

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
            Path = FullPath(file, requested ?? ConfigurationFile.DefaultFileName),
            Optional = requested is null,
            Exists = file is { FileProvider: IFileProvider provider, Path: string path } && provider.GetFileInfo(path).Exists,
            Failure = failure,
        };
    }

    #region Helpers

    // The provider's root is where a relative path is read from: the content root, or the directory of a rooted path.
    private static string FullPath(JsonConfigurationSource? source, string given)
    {
        if (source is { FileProvider: PhysicalFileProvider provider, Path: string path }) return Path.GetFullPath(Path.Combine(provider.Root, path));

        return Path.GetFullPath(given);
    }

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
