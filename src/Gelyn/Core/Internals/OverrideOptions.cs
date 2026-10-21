using System.CommandLine;

namespace Gelyn.Core.Internals;

/// <summary>
///     Options that override a configured setting for the duration of one run.
/// </summary>
/// <remarks>
///     Shared so that the command declaring an option and the configuration layer promoting it cannot drift
///     apart. The names carry no leading dashes, because that is the form the command line configuration
///     provider reports them under.
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
    ///     The configuration index the first promoted exclude pattern is written at.
    /// </summary>
    /// <remarks>
    ///     Configuration merges an array per index rather than replacing it, so writing at index zero would
    ///     overwrite the first configured pattern and leave the rest of the configured list in place. Keys
    ///     are ordered numerically, so an offset no configured list will reach appends instead.
    /// </remarks>
    internal const int ExcludeOffset = 1_000;

    /// <summary>
    ///     The command line options these overrides arrive through, one per overridable setting.
    /// </summary>
    /// <remarks>
    ///     These options carry values into <c>SiteOptions</c> through <c>AddSiteConfiguration</c>, and no
    ///     command reads them from the parse result. They are declared here rather than in a command, which
    ///     keeps the names and the options together. Every read returns new instances, because an
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
        new Option<string>($"--{Content}")
        {
            Description = "The directory the site is built from.",
            HelpName = "directory",
        },
        new Option<string>($"--{Output}")
        {
            Description = "The directory the generated site is written to.",
            HelpName = "directory",
        },
        new Option<string>($"--{Exclude}")
        {
            Description = $"Further name patterns to skip, separated by '{ExcludeSeparator}'.",
            HelpName = "patterns",
        },
    ];
}
