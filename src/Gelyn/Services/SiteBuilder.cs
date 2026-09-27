using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
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
    private readonly IFileSystem _fileSystem;

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
    /// <param name="fileSystem">
    ///     Writes the generated pages.
    /// </param>
    public SiteBuilder(
        ContentWalker walker,
        IHostEnvironment environment,
        IOptionsMonitor<SiteOptions> options,
        IFileSystem fileSystem)
    {
        this._walker = walker;
        this._environment = environment;
        this._options = options;
        this._fileSystem = fileSystem;
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

        IReadOnlyList<ContentPage> navigation = [.. pages.Where(static page => page.InNavigation)];

        IPath path = this._fileSystem.Path;
        string outputDirectory = path.Combine(this._environment.ContentRootPath, options.OutputDirectory);
        var written = new List<string>(pages.Count);

        foreach (ContentPage page in pages)
        {
            var context = new RenderContext
            {
                Options = options,
                Navigation = navigation,
                Page = page,
            };

            string html = PageLayout.Render(context);
            string destination = path.Combine(outputDirectory, page.OutputPath);

            this._fileSystem.Directory.CreateDirectory(path.GetDirectoryName(destination) ?? outputDirectory);

            await this._fileSystem.File
                .WriteAllTextAsync(destination, html, cancellationToken)
                .ConfigureAwait(false);

            written.Add(page.OutputPath);
        }

        return written;
    }
}
