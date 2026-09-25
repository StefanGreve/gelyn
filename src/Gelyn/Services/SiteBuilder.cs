using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Gelyn.Abstractions;
using Gelyn.Model.Options;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Gelyn.Services;

/// <summary>
///     Runs every registered page builder and writes the results to the output directory.
/// </summary>
public sealed class SiteBuilder
{
    private readonly IEnumerable<PageBuilderContract> _pageBuilders;
    private readonly IHostEnvironment _environment;
    private readonly IOptionsMonitor<SiteOptions> _options;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SiteBuilder"/> class.
    /// </summary>
    /// <param name="pageBuilders">
    ///     Every page the site is made of.
    /// </param>
    /// <param name="environment">
    ///     Supplies the root that a relative output directory is resolved against.
    /// </param>
    /// <param name="options">
    ///     Supplies the output directory.
    /// </param>
    public SiteBuilder(
        IEnumerable<PageBuilderContract> pageBuilders,
        IHostEnvironment environment,
        IOptionsMonitor<SiteOptions> options)
    {
        this._pageBuilders = pageBuilders;
        this._environment = environment;
        this._options = options;
    }

    /// <summary>
    ///     Generates the whole site.
    /// </summary>
    /// <param name="cancellationToken">
    ///     Token used to cancel the operation.
    /// </param>
    /// <returns>
    ///     The output-relative paths that were written.
    /// </returns>
    public async Task<IReadOnlyList<string>> BuildAsync(CancellationToken cancellationToken)
    {
        SiteOptions options = this._options.CurrentValue;

        List<string> written = [];
        string outputDirectory = Path.Combine(this._environment.ContentRootPath, options.OutputDirectory);

        foreach (PageBuilderContract pageBuilder in this._pageBuilders)
        {
            string html = await pageBuilder.BuildAsync(cancellationToken).ConfigureAwait(false);
            string destination = Path.Combine(outputDirectory, pageBuilder.OutputPath);

            Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? outputDirectory);
            await File.WriteAllTextAsync(destination, html, cancellationToken).ConfigureAwait(false);

            written.Add(pageBuilder.OutputPath);
        }

        return written;
    }
}
