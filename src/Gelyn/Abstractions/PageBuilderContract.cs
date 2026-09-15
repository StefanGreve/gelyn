using System.Threading;
using System.Threading.Tasks;

namespace Gelyn.Abstractions;

/// <summary>
///     Builds one page of the site.
/// </summary>
/// <remarks>
///     Implementations return markup rather than writing it, so that all file output stays in one place.
/// </remarks>
public abstract class PageBuilderContract
{
    /// <summary>
    ///     The path of the generated file, relative to the output root.
    /// </summary>
    public abstract string OutputPath { get; }

    /// <summary>
    ///     Builds the page.
    /// </summary>
    /// <param name="cancellationToken">
    ///     Token used to cancel the operation.
    /// </param>
    /// <returns>
    ///     The complete HTML document.
    /// </returns>
    public abstract Task<string> BuildAsync(CancellationToken cancellationToken);
}
