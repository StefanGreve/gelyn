using Gelyn.Model;

using Microsoft.Extensions.Options;

using SharpYaml;

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
    /// <exception cref="FileNotFoundException">
    ///     The file given to <c>--config</c> or the landing page does not exist.
    /// </exception>
    /// <exception cref="InvalidDataException">
    ///     The configuration file is not valid JSON.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///     A configured value cannot be converted to the type of its setting.
    /// </exception>
    /// <exception cref="OptionsValidationException">
    ///     A setting holds a value its validation rejects.
    /// </exception>
    /// <exception cref="YamlException">
    ///     A front matter block is not well-formed YAML, or declares a stylesheet or a script the scan did not
    ///     locate.
    /// </exception>
    /// <exception cref="IOException">
    ///     The output directory is a link, one path is published twice, or a file cannot be written or removed.
    /// </exception>
    public abstract Task<BuildReport> BuildAsync(CancellationToken cancellationToken);
}
