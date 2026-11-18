using System.CommandLine;
using System.CommandLine.Parsing;

using Gelyn.Core.Internals;
using Gelyn.Model;
using Gelyn.Model.Options;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.FileProviders;

namespace Gelyn.Extensions;

/// <summary>
///     Configuration sources the composition root in <see cref="Program"/> layers on top of the <c>DOTNET_</c>
///     environment variables it seeds the host configuration with.
/// </summary>
internal static class ConfigurationManagerExtensions
{
    // Prefixed, because names such as LANGUAGE, PORT and TITLE are set by desktops and hosts for their own use.
    private const string SettingVariablePrefix = "GELYN_";

    /// <summary>
    ///     Adds the tool's configuration file and the environment variables that outrank it.
    /// </summary>
    /// <remarks>
    ///     A missing or unparsable file is returned as a failure instead of thrown, so that <c>--help</c>,
    ///     <c>--version</c> and <c>--info</c> still run. A file that cannot be opened at all still throws,
    ///     because the provider opens it outside its error handler. The file is not watched, because the watch
    ///     costs most of the startup and <c>serve</c> runs its own watcher.
    /// </remarks>
    /// <param name="configuration">
    ///     The configuration to add the sources to.
    /// </param>
    /// <param name="requested">
    ///     The path given to <see cref="ConfigurationFile.OptionName"/>, as <see cref="ConfigurationFile.ReadPath"/>
    ///     reads it from the parsed command line, or <see langword="null"/> for the default file.
    /// </param>
    /// <returns>
    ///     What the first load of the file found, including the failure it held back.
    /// </returns>
    public static ConfigurationFileStatus AddSiteConfiguration(this ConfigurationManager configuration, string? requested)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // Rooted, so that the source reads it through a provider of its own directory: the base directory's provider
        // refuses a path that climbs out of it, such as ../shared.json.
        string fullPath = Path.GetFullPath(requested ?? ConfigurationFile.DefaultFileName, configuration.ResolveBaseDirectory());

        JsonConfigurationSource? file = null;
        Exception? failure = null;

        configuration.AddJsonFile(source =>
        {
            source.Path = fullPath;
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

        var overrides = new OverrideSource();

        // Recorded, so that a later reader such as the serve watcher sees the path the loader was given.
        overrides.Provider.Set(ConfigurationFile.PathKey, requested);

        configuration
            .AddEnvironmentVariables(prefix: SettingVariablePrefix)
            .Add(overrides);

        return new ConfigurationFileStatus
        {
            Path = fullPath,
            Optional = requested is null,
            Exists = file is { FileProvider: IFileProvider provider, Path: string path } && provider.GetFileInfo(path).Exists,
            Failure = failure,
        };
    }

    /// <summary>
    ///     Applies the settings the command line overrides, which outrank every other source.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Read from the System.CommandLine parse, because only the parse knows the syntax the tool accepts:
    ///         <c>--option:value</c>, <c>--option=value</c>, response files, and options that take no value.
    ///     </para>
    ///     <para>
    ///         Written into the last source <see cref="AddSiteConfiguration"/> added, because adding a source once
    ///         the host is built raises a reload, and the options monitor answers a reload by binding and validating
    ///         at once. That would surface a configuration failure before <c>--help</c> or <c>--info</c> runs.
    ///     </para>
    /// </remarks>
    /// <param name="configuration">
    ///     The configuration <see cref="AddSiteConfiguration"/> was called on.
    /// </param>
    /// <param name="parseResult">
    ///     The parse of the full command tree.
    /// </param>
    /// <exception cref="InvalidOperationException">
    ///     <see cref="AddSiteConfiguration"/> was not called on <paramref name="configuration"/>.
    /// </exception>
    public static void ApplyOverrides(this ConfigurationManager configuration, ParseResult parseResult)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(parseResult);

        if (configuration.Sources.OfType<OverrideSource>().LastOrDefault() is not OverrideSource overrides)
            throw new InvalidOperationException($"{nameof(AddSiteConfiguration)} must be called before the overrides are applied.");

        foreach ((string key, string? value) in ReadOverrides(parseResult))
            overrides.Provider.Set(key, value);
    }

    #region Helpers

    // The host points the builder's file provider at the content root.
    private static string ResolveBaseDirectory(this ConfigurationManager configuration) =>
        configuration.GetFileProvider() is PhysicalFileProvider provider ? provider.Root : Directory.GetCurrentDirectory();

    private static Dictionary<string, string?> ReadOverrides(ParseResult parseResult)
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
            if (ReadValue(parseResult, option) is string value) overrides[key] = value;
        }

        if (ReadValue(parseResult, OverrideOptions.Exclude) is string patterns)
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

    // The token as given, so that the binder converts it as it converts the same value from the file.
    private static string? ReadValue(ParseResult parseResult, string option) =>
        parseResult.GetResult($"--{option}") is OptionResult { Implicit: false, Tokens: [.., Token token] } ? token.Value : null;

    #endregion

    // Hands out one provider, so that a value written after the build reaches it without the reload of a new source.
    private sealed class OverrideSource : IConfigurationSource
    {
        internal MemoryConfigurationProvider Provider { get; } = new(new MemoryConfigurationSource());

        public IConfigurationProvider Build(IConfigurationBuilder builder) => this.Provider;
    }
}
