using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using Gelyn.Internals;

using Microsoft.Extensions.Options;

namespace Gelyn.Model.Options;

/// <summary>
///     Checks that <see cref="SiteOptions"/> carries usable values before anything consumes it.
/// </summary>
/// <remarks>
///     Written by hand rather than delegated to <c>ValidateDataAnnotations</c>, which is annotated
///     <c>RequiresUnreferencedCode</c> and cannot see members the trimmer may have removed.
/// </remarks>
[SuppressMessage("Performance", "CA1812", Justification = Justifications.ByDesign)]
internal sealed class SiteOptionsValidator : IValidateOptions<SiteOptions>
{
    /// <inheritdoc/>
    [SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = Justifications.ByDesign)]
    public ValidateOptionsResult Validate(string? name, SiteOptions options)
    {
        List<string> failures = [];

        if (string.IsNullOrWhiteSpace(options.Title))
            failures.Add($"{nameof(SiteOptions.Title)} must not be empty.");

        if (string.IsNullOrWhiteSpace(options.ContentDirectory))
            failures.Add($"{nameof(SiteOptions.ContentDirectory)} must not be empty.");

        if (string.IsNullOrWhiteSpace(options.OutputDirectory))
            failures.Add($"{nameof(SiteOptions.OutputDirectory)} must not be empty.");

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}