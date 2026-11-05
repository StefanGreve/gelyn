using Gelyn.Core.Internals;
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
internal sealed class SiteOptionsValidator : IValidateOptions<SiteOptions>
{
    private readonly IHostEnvironment _environment;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SiteOptionsValidator"/> class.
    /// </summary>
    /// <param name="environment">
    ///     The host environment the directory settings are resolved against.
    /// </param>
    public SiteOptionsValidator(IHostEnvironment environment)
    {
        this._environment = environment;
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

        if (string.IsNullOrWhiteSpace(options.ContentDirectory))
            failures.Add($"{nameof(SiteOptions.ContentDirectory)} must not be empty.");

        if (string.IsNullOrWhiteSpace(options.OutputDirectory))
            failures.Add($"{nameof(SiteOptions.OutputDirectory)} must not be empty.");

        // An empty version addresses no package, since it goes into the URL as katex@/dist.
        if (string.IsNullOrWhiteSpace(options.KaTeXVersion))
            failures.Add($"{nameof(SiteOptions.KaTeXVersion)} must not be empty.");

        if (string.IsNullOrWhiteSpace(options.MermaidVersion))
            failures.Add($"{nameof(SiteOptions.MermaidVersion)} must not be empty.");

        if (options.BaseUrl is Uri baseUrl && !IsServable(baseUrl))
            failures.Add($"{nameof(SiteOptions.BaseUrl)} must be an absolute http or https URL, such as 'https://example.com/blog'.");

        for (int i = 0; i < options.FooterLinks.Count; i++)
        {
            FooterLink link = options.FooterLinks[i];
            string key = $"{nameof(SiteOptions.FooterLinks)}[{i}]";

            if (string.IsNullOrWhiteSpace(link.Label))
                failures.Add($"{key}.{nameof(FooterLink.Label)} must not be empty.");

            if (link.Url is not Uri url || !IsServable(url))
                failures.Add($"{key}.{nameof(FooterLink.Url)} must be an absolute http or https URL, such as 'https://github.com/octocat'.");
        }

        if (!string.IsNullOrWhiteSpace(options.ContentDirectory) && !string.IsNullOrWhiteSpace(options.OutputDirectory)
            && ViolatesBoundary(this._environment.ContentRootPath, options.ContentDirectory, options.OutputDirectory))
        {
            failures.Add($"{nameof(SiteOptions.OutputDirectory)} must not resolve to, inside, or above {nameof(SiteOptions.ContentDirectory)}.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    #region Helpers

    // A relative URL carries no origin to resolve a link against, and Scheme throws when read from one. The
    // scheme test is also what keeps a javascript: or data: footer link out of an anchor the build publishes.
    private static bool IsServable(Uri url) =>
        url.IsAbsoluteUri && (url.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.Ordinal)
            || url.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.Ordinal));

    // The build would publish an output inside the content directory as content, `gelyn clean` would remove
    // a content directory inside the output, and `gelyn serve` would retrigger its own watcher on what it
    // just wrote, so neither may contain the other.
    private static bool ViolatesBoundary(string root, string content, string output) =>
        PathBoundary.Overlap(PathBoundary.Resolve(root, content), PathBoundary.Resolve(root, output));

    #endregion
}
