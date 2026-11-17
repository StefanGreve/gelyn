using System.Net;
using System.Text.RegularExpressions;

using Gelyn.Core;
using Gelyn.Core.Internals;
using Gelyn.Model;
using Gelyn.Model.Options;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Gelyn.Validators;

/// <summary>
///     Checks that <see cref="SiteOptions"/> carries usable values before anything consumes it.
/// </summary>
/// <remarks>
///     Written by hand, because <c>ValidateDataAnnotations</c> is annotated <c>RequiresUnreferencedCode</c>
///     and cannot see members the trimmer may have removed.
/// </remarks>
internal sealed partial class SiteOptionsValidator : IValidateOptions<SiteOptions>
{
    private readonly IHostEnvironment _environment;
    private readonly ConfigurationFileStatus _file;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SiteOptionsValidator"/> class.
    /// </summary>
    /// <param name="environment">
    ///     The host environment the directory settings are resolved against.
    /// </param>
    /// <param name="file">
    ///     The configuration file the run reads, which the output directory must not hold.
    /// </param>
    public SiteOptionsValidator(IHostEnvironment environment, ConfigurationFileStatus file)
    {
        this._environment = environment;
        this._file = file;
    }

    /// <inheritdoc/>
    [SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = Justifications.ByDesign)]
    public ValidateOptionsResult Validate(string? name, SiteOptions options)
    {
        List<string> failures = [];

        if (string.IsNullOrWhiteSpace(options.Title))
            failures.Add($"{nameof(SiteOptions.Title)} must not be empty.");

        // Author and Description are optional, but an empty lang attribute is worse than none at all.
        if (string.IsNullOrWhiteSpace(options.Language))
            failures.Add($"{nameof(SiteOptions.Language)} must not be empty.");
        else if (!LanguageTagRegex().IsMatch(options.Language))
            failures.Add($"{nameof(SiteOptions.Language)} must be a BCP 47 language tag, such as 'en' or 'de-DE'.");

        failures.AddRange(this.ValidateDirectories(options.ContentDirectory, options.OutputDirectory));

        // An empty version addresses no package, since it goes into the URL as katex@/dist.
        if (string.IsNullOrWhiteSpace(options.KaTeXVersion))
            failures.Add($"{nameof(SiteOptions.KaTeXVersion)} must not be empty.");

        if (string.IsNullOrWhiteSpace(options.MermaidVersion))
            failures.Add($"{nameof(SiteOptions.MermaidVersion)} must not be empty.");

        if (string.IsNullOrWhiteSpace(options.ShikiVersion))
            failures.Add($"{nameof(SiteOptions.ShikiVersion)} must not be empty.");

        if (options.Giscus is GiscusOptions giscus)
            failures.AddRange(ValidateGiscusOptions(giscus));

        if (options.BaseUrl is Uri baseUrl && !IsServable(baseUrl))
            failures.Add($"{nameof(SiteOptions.BaseUrl)} must be an absolute http or https URL, such as 'https://example.com/blog'.");

        failures.AddRange(ValidateFooterLinks(options.FooterLinks));

        if (options.Theme is string theme && !ThemeCatalog.Names.Contains(theme, StringComparer.Ordinal))
            failures.Add($"{nameof(SiteOptions.Theme)} must name a bundled theme: {string.Join(", ", ThemeCatalog.Names)}.");

        // Caught here, since the bind would turn a bad port into an opaque HttpListener error code.
        if (options.Port is <= IPEndPoint.MinPort or > IPEndPoint.MaxPort)
            failures.Add($"{nameof(SiteOptions.Port)} must be between {IPEndPoint.MinPort + 1} and {IPEndPoint.MaxPort}.");

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    #region Helpers

    private IEnumerable<string> ValidateDirectories(string content, string output)
    {
        if (string.IsNullOrWhiteSpace(content))
            yield return $"{nameof(SiteOptions.ContentDirectory)} must not be empty.";

        if (string.IsNullOrWhiteSpace(output))
            yield return $"{nameof(SiteOptions.OutputDirectory)} must not be empty.";

        if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(output)) yield break;

        string root = PathBoundary.Resolve(this._environment.ContentRootPath);
        string resolved = PathBoundary.Resolve(root, output);

        if (ViolatesBoundary(root, content, output))
            yield return $"{nameof(SiteOptions.OutputDirectory)} must not resolve to, inside, or above {nameof(SiteOptions.ContentDirectory)}.";
        else if (EnclosesPath(resolved, root))
            yield return $"{nameof(SiteOptions.OutputDirectory)} must not resolve to or above the content root.";
        else if (EnclosesPath(resolved, this._file.Path))
            yield return $"{nameof(SiteOptions.OutputDirectory)} must not contain the configuration file '{this._file.Path}'.";
    }

    private static IEnumerable<string> ValidateGiscusOptions(GiscusOptions giscus)
    {
        string key = nameof(SiteOptions.Giscus);

        if (!IsRepository(giscus.Repo))
            yield return $"{key}.{nameof(GiscusOptions.Repo)} must name a repository as 'owner/name'.";

        if (string.IsNullOrWhiteSpace(giscus.RepoId))
            yield return $"{key}.{nameof(GiscusOptions.RepoId)} must not be empty.";

        if (string.IsNullOrWhiteSpace(giscus.Category))
            yield return $"{key}.{nameof(GiscusOptions.Category)} must not be empty.";

        if (string.IsNullOrWhiteSpace(giscus.CategoryId))
            yield return $"{key}.{nameof(GiscusOptions.CategoryId)} must not be empty.";
    }

    private static IEnumerable<string> ValidateFooterLinks(IReadOnlyList<FooterLink> links)
    {
        for (int i = 0; i < links.Count; i++)
        {
            FooterLink link = links[i];
            string key = $"{nameof(SiteOptions.FooterLinks)}[{i}]";

            if (string.IsNullOrWhiteSpace(link.Label))
                yield return $"{key}.{nameof(FooterLink.Label)} must not be empty.";

            if (link.Url is not Uri url || !IsServable(url))
                yield return $"{key}.{nameof(FooterLink.Url)} must be an absolute http or https URL, such as 'https://github.com/octocat'.";
        }
    }

    // A relative URL carries no origin to resolve a link against, and Scheme throws when read from one. The
    // scheme test is also what keeps a javascript: or data: footer link out of an anchor the build publishes.
    private static bool IsServable(Uri url) =>
        url.IsAbsoluteUri && (url.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.Ordinal)
            || url.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.Ordinal));

    // giscus hands the value to GitHub as owner/name, so a missing half or a third segment addresses nothing.
    private static bool IsRepository(string? repo) =>
        repo?.Split('/') is [{ Length: > 0 }, { Length: > 0 }] && !repo.Any(char.IsWhiteSpace);

    // The same pattern the schema declares, so an editor and the build reject the same values.
    [GeneratedRegex(SiteOptions.LanguageTagPattern)]
    private static partial Regex LanguageTagRegex();

    // The build would publish an output inside the content directory as content, `gelyn clean` would remove
    // a content directory inside the output, and `gelyn serve` would retrigger its own watcher on what it
    // just wrote, so neither may contain the other.
    private static bool ViolatesBoundary(string root, string content, string output) =>
        PathBoundary.Overlap(PathBoundary.Resolve(root, content), PathBoundary.Resolve(root, output));

    // A build deletes every file in the output directory it did not write, and `gelyn clean` deletes the
    // directory itself. Case insensitive like Overlap, because rejecting too much is the safe direction.
    private static bool EnclosesPath(string directory, string path) =>
        path.Equals(directory, StringComparison.OrdinalIgnoreCase) || StringUtils.StartsWithSegment(path, directory, Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    #endregion
}
