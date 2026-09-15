using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Gelyn.Abstractions;
using Gelyn.Internals;

using Microsoft.Extensions.Options;

namespace Gelyn.Services;

/// <summary>
///     Runs every registered page builder and writes the results to the output directory.
/// </summary>
[SuppressMessage("Performance", "CA1812", Justification = Justifications.ByDesign)]
internal sealed class SiteBuilder
{
    private readonly IEnumerable<PageBuilderContract> _pageBuilders;
    private readonly SiteOptions _options;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SiteBuilder"/> class.
    /// </summary>
    /// <param name="pageBuilders">
    ///     Every page the site is made of.
    /// </param>
    /// <param name="options">
    ///     Supplies the output root.
    /// </param>
    public SiteBuilder(IEnumerable<PageBuilderContract> pageBuilders, IOptions<SiteOptions> options)
    {
        this._pageBuilders = pageBuilders;
        this._options = options.Value;
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
        List<string> written = [];

        foreach (PageBuilderContract pageBuilder in this._pageBuilders)
        {
            string html = await pageBuilder.BuildAsync(cancellationToken).ConfigureAwait(false);
            string destination = Path.Combine(this._options.OutputDirectory, pageBuilder.OutputPath);

            Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? this._options.OutputDirectory);
            await File.WriteAllTextAsync(destination, html, cancellationToken).ConfigureAwait(false);

            written.Add(pageBuilder.OutputPath);
        }

        return written;
    }
}
