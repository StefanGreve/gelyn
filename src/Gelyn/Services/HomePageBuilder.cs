using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Gelyn.Abstractions;
using Gelyn.Internals;
using Gelyn.Model;
using Gelyn.Model.Options;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Gelyn.Services;

/// <summary>
///     Builds the landing page from <c>index.md</c>.
/// </summary>
public sealed class HomePageBuilder : PageBuilderContract
{
    private const string SourceFileName = "index.md";

    private readonly MarkdownRendererContract _renderer;
    private readonly PageLayout _layout;
    private readonly IHostEnvironment _environment;
    private readonly SiteOptions _options;

    /// <summary>
    ///     Initializes a new instance of the <see cref="HomePageBuilder"/> class.
    /// </summary>
    /// <param name="renderer">
    ///     Converts the Markdown source to HTML.
    /// </param>
    /// <param name="layout">
    ///     Wraps the rendered content in the document shell.
    /// </param>
    /// <param name="environment">
    ///     Supplies the root that a relative content directory is resolved against.
    /// </param>
    /// <param name="options">
    ///     Supplies the content directory.
    /// </param>
    [SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = Justifications.ByDesign)]
    public HomePageBuilder(
        MarkdownRendererContract renderer,
        PageLayout layout,
        IHostEnvironment environment,
        IOptions<SiteOptions> options)
    {
        this._renderer = renderer;
        this._layout = layout;
        this._environment = environment;
        this._options = options.Value;
    }

    /// <inheritdoc/>
    public override string OutputPath => "index.html";

    /// <inheritdoc/>
    public override async Task<string> BuildAsync(CancellationToken cancellationToken)
    {
        string source = Path.Combine(this._environment.ContentRootPath, this._options.ContentDirectory, SourceFileName);

        if (!File.Exists(source))
            throw new FileNotFoundException($"No landing page found at '{source}'.", source);

        string markdown = await File.ReadAllTextAsync(source, cancellationToken).ConfigureAwait(false);
        RenderedMarkdown rendered = this._renderer.Render(markdown);

        return this._layout.Render(rendered.MetaData?.Title, rendered.Html ?? string.Empty);
    }
}