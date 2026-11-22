using Gelyn.Exceptions;
using Gelyn.Model;
using Gelyn.Model.Options;

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
    /// <param name="options">
    ///     The settings the site is built against.
    /// </param>
    /// <param name="cancellationToken">
    ///     Token used to cancel the operation.
    /// </param>
    /// <returns>
    ///     What the build produced, and how long it took.
    /// </returns>
    /// <exception cref="ConfigurationException">
    ///     The content directory publishes no landing page.
    /// </exception>
    /// <exception cref="YamlException">
    ///     A front matter block is not well-formed YAML.
    /// </exception>
    /// <exception cref="BuildException">
    ///     The output directory is a link, one path is published twice, or a front matter block declares a
    ///     stylesheet or a script the scan did not locate.
    /// </exception>
    /// <exception cref="IOException">
    ///     A file cannot be written or removed.
    /// </exception>
    public abstract Task<BuildReport> BuildAsync(SiteOptions options, CancellationToken cancellationToken);
}
