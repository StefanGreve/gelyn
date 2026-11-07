using Gelyn.Model;

namespace Gelyn.Abstractions;

/// <summary>
///     Writes the finished site to the output directory.
/// </summary>
internal abstract class SiteBuilderContract
{
    /// <summary>
    ///     Generates the whole site.
    /// </summary>
    /// <param name="cancellationToken">
    ///     Token used to cancel the operation.
    /// </param>
    /// <returns>
    ///     What the build produced, and how long it took.
    /// </returns>
    public abstract Task<BuildReport> BuildAsync(CancellationToken cancellationToken);
}
