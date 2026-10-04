using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;

using Gelyn.Internals;
using Gelyn.Model.Options;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Gelyn.Validators;

/// <summary>
///     Checks that <see cref="SiteOptions"/> carries usable values before anything consumes it.
/// </summary>
/// <remarks>
///     Written by hand rather than delegated to <c>ValidateDataAnnotations</c>, which is annotated
///     <c>RequiresUnreferencedCode</c> and cannot see members the trimmer may have removed.
/// </remarks>
public sealed class SiteOptionsValidator : IValidateOptions<SiteOptions>
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

        if (options.BaseUrl is Uri baseUrl && !IsServable(baseUrl))
            failures.Add($"{nameof(SiteOptions.BaseUrl)} must be an absolute http or https URL, such as 'https://example.com/blog'.");

        if (!string.IsNullOrWhiteSpace(options.ContentDirectory) && !string.IsNullOrWhiteSpace(options.OutputDirectory)
            && IsNested(this._environment.ContentRootPath, options.ContentDirectory, options.OutputDirectory))
        {
            failures.Add($"{nameof(SiteOptions.OutputDirectory)} must not resolve to or inside {nameof(SiteOptions.ContentDirectory)}.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    #region Helpers

    // A relative URL carries no origin to resolve a link against, and Scheme throws when read from one.
    private static bool IsServable(Uri baseUrl) =>
        baseUrl.IsAbsoluteUri && (baseUrl.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.Ordinal)
            || baseUrl.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.Ordinal));

    private static bool IsNested(string root, string content, string output)
    {
        string contentPath = Normalize(root, content);
        string outputPath = Normalize(root, output);

        return outputPath.Equals(contentPath, StringComparison.OrdinalIgnoreCase)
            || outputPath.StartsWith(contentPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string root, string directory) =>
        Path.GetFullPath(Path.Combine(root, directory)).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    #endregion
}
