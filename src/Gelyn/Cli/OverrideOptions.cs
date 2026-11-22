using System.CommandLine;
using System.CommandLine.Parsing;

using Gelyn.Core;
using Gelyn.Exceptions;
using Gelyn.Model.Options;

namespace Gelyn.Cli;

/// <summary>
///     Options that override a configured setting for the duration of one run.
/// </summary>
/// <remarks>
///     Declared and applied in one place, so that the command declaring an option and the settings reading it
///     cannot drift apart. The names carry no leading dashes, so that each can name both the option and the
///     setting it stands for.
/// </remarks>
internal static class OverrideOptions
{
    /// <summary>
    ///     Overrides the site title.
    /// </summary>
    internal const string Title = "title";

    /// <summary>
    ///     Overrides the URL the finished site is served from.
    /// </summary>
    internal const string BaseUrl = "base-url";

    /// <summary>
    ///     Overrides the bundled theme every page is styled with.
    /// </summary>
    internal const string Theme = "theme";

    /// <summary>
    ///     Overrides whether the build writes a syndication feed.
    /// </summary>
    internal const string Feed = "feed";

    /// <summary>
    ///     Overrides whether the build writes a sitemap.
    /// </summary>
    internal const string Sitemap = "sitemap";

    /// <summary>
    ///     Overrides the content directory.
    /// </summary>
    internal const string Content = "content";

    /// <summary>
    ///     Overrides the output directory.
    /// </summary>
    internal const string Output = "output";

    /// <summary>
    ///     Adds to the configured exclude patterns, as one value holding
    ///     <see cref="ExcludeSeparator"/>-separated patterns.
    /// </summary>
    internal const string Exclude = "exclude";

    /// <summary>
    ///     Separates the patterns within a single <see cref="Exclude"/> value.
    /// </summary>
    internal const char ExcludeSeparator = ',';

    /// <summary>
    ///     Overrides the port <c>gelyn serve</c> listens on.
    /// </summary>
    /// <remarks>
    ///     Not among <see cref="Definitions"/>, because only <c>serve</c> declares it.
    /// </remarks>
    internal const string Port = "port";

    /// <summary>
    ///     The command line options these overrides arrive through, one per setting every building command
    ///     overrides.
    /// </summary>
    /// <remarks>
    ///     These options carry values into <see cref="SiteOptions"/> through <see cref="Apply"/>, and no
    ///     command reads them from the parse result. Every read returns new instances, because an
    ///     <see cref="Option"/> belongs to one command tree.
    /// </remarks>
    internal static IReadOnlyList<Option> Definitions =>
    [
        new Option<string>($"--{Title}")
        {
            Description = "The name of the site.",
            HelpName = "name",
        },
        new Option<string>($"--{BaseUrl}")
        {
            Description = "The absolute URL the finished site is served from.",
            HelpName = "url",
        },
        new Option<string>($"--{Theme}") // Checked by SiteOptionsValidator, not AcceptOnlyFromAmong, for one exit code
        {
            Description = $"The bundled theme to style every page with: {string.Join(", ", ThemeCatalog.Names)}.",
            HelpName = "name",
            CompletionSources = { static _ => ThemeCatalog.Names },
        },
        new Option<bool>($"--{Feed}")
        {
            Description = "Whether to write a syndication feed of the pages that ask to be in one.",
            HelpName = "true|false",
            Arity = ArgumentArity.ExactlyOne,
        },
        new Option<bool>($"--{Sitemap}")
        {
            Description = "Whether to write a sitemap of the pages the site publishes.",
            HelpName = "true|false",
            Arity = ArgumentArity.ExactlyOne,
        },
        CreateContentOption(),
        CreateOutputOption("The directory the generated site is written to. A build deletes every file in it that it did not write, except dot-prefixed entries and links."),
        new Option<string>($"--{Exclude}")
        {
            Description = $"Further name patterns to skip, separated by '{ExcludeSeparator}'.",
            HelpName = "patterns",
        },
    ];

    /// <summary>
    ///     The command line options that override the content and the output directory, which is the subset
    ///     <c>clean</c> declares.
    /// </summary>
    /// <remarks>
    ///     Every read returns new instances, as for <see cref="Definitions"/>.
    /// </remarks>
    internal static IReadOnlyList<Option> DirectoryDefinitions =>
    [
        CreateContentOption(),
        CreateOutputOption("The directory to remove."),
    ];

    /// <summary>
    ///     Applies every override the command line carries to the bound options, as typed values.
    /// </summary>
    /// <remarks>
    ///     <see cref="Exclude"/> adds its patterns to the configured list, where every other override replaces
    ///     the configured value.
    /// </remarks>
    /// <param name="parseResult">
    ///     The parse of the command line, without errors.
    /// </param>
    /// <param name="options">
    ///     The options bound from the file and the variables, which the overrides are written into.
    /// </param>
    /// <exception cref="ConfigurationException">
    ///     The value given to <see cref="BaseUrl"/> is not a URL.
    /// </exception>
    internal static void Apply(ParseResult parseResult, SiteOptions options)
    {
        ArgumentNullException.ThrowIfNull(parseResult);
        ArgumentNullException.ThrowIfNull(options);

        if (ReadText(parseResult, Title) is string title) options.Title = title;
        if (ReadText(parseResult, BaseUrl) is string baseUrl) options.BaseUrl = ConvertBaseUrl(baseUrl);
        if (ReadText(parseResult, Theme) is string theme) options.Theme = theme;
        if (IsGiven(parseResult, Feed)) options.EnableFeed = parseResult.GetValue<bool>($"--{Feed}");
        if (IsGiven(parseResult, Sitemap)) options.EnableSitemap = parseResult.GetValue<bool>($"--{Sitemap}");
        if (ReadText(parseResult, Content) is string content) options.ContentDirectory = content;
        if (ReadText(parseResult, Output) is string output) options.OutputDirectory = output;
        if (IsGiven(parseResult, Port)) options.Port = parseResult.GetValue<int>($"--{Port}");

        if (ReadText(parseResult, Exclude) is string patterns)
            options.ExcludePatterns = [.. options.ExcludePatterns, .. patterns.Split(ExcludeSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }

    #region Helpers

    // Looked up by name, because a command declares only the subset it takes, and an absent symbol has no result.
    private static bool IsGiven(ParseResult parseResult, string option) =>
        parseResult.GetResult($"--{option}") is OptionResult { Implicit: false };

    private static string? ReadText(ParseResult parseResult, string option) =>
        IsGiven(parseResult, option) ? parseResult.GetValue<string>($"--{option}") : null;

    // Converted as the binder converts the same value from the file, and rejected with the binder's message.
    private static Uri ConvertBaseUrl(string value)
    {
        try
        {
            return new Uri(value, UriKind.RelativeOrAbsolute);
        }
        catch (UriFormatException exception)
        {
            throw new ConfigurationException([$"Failed to convert configuration value '{value}' at '{nameof(SiteOptions.BaseUrl)}' to type '{typeof(Uri)}'."], exception);
        }
    }

    private static Option<string> CreateContentOption() => new($"--{Content}")
    {
        Description = "The directory the site is built from.",
        HelpName = "directory",
    };

    private static Option<string> CreateOutputOption(string description) => new($"--{Output}")
    {
        Description = description,
        HelpName = "directory",
    };

    #endregion
}
