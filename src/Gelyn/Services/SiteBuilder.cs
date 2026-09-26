using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Gelyn.Model;
using Gelyn.Model.Options;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Gelyn.Services;

/// <summary>
///     Renders every discovered page and writes the results to the output directory.
/// </summary>
public sealed class SiteBuilder
{
    private readonly ContentWalker _walker;
    private readonly IHostEnvironment _environment;
    private readonly IOptionsMonitor<SiteOptions> _options;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SiteBuilder"/> class.
    /// </summary>
    /// <param name="walker">
    ///     Discovers and renders the pages the site is made of.
    /// </param>
    /// <param name="environment">
    ///     Supplies the root that a relative output directory is resolved against.
    /// </param>
    /// <param name="options">
    ///     Supplies the output directory.
    /// </param>
    public SiteBuilder(
        ContentWalker walker,
        IHostEnvironment environment,
        IOptionsMonitor<SiteOptions> options)
    {
        this._walker = walker;
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

        IReadOnlyList<ContentPage> pages = await this._walker
            .WalkAsync(options, cancellationToken)
            .ConfigureAwait(false);

        string outputDirectory = Path.Combine(this._environment.ContentRootPath, options.OutputDirectory);
        var written = new List<string>(pages.Count);

        foreach (ContentPage page in pages)
        {
            var context = new RenderContext
            {
                Options = options,
                Pages = pages,
                Page = page,
            };

            string html = PageLayout.Render(context);
            string destination = Path.Combine(outputDirectory, page.OutputPath);

            Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? outputDirectory);
            await File.WriteAllTextAsync(destination, html, cancellationToken).ConfigureAwait(false);

            written.Add(page.OutputPath);
        }

        return written;
    }
}
