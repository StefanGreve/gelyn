using Gelyn.Model;
using Gelyn.Model.Options;

using SharpYaml;

namespace Gelyn.Abstractions;

/// <summary>
///     Discovers the files the site is built from, and renders the Markdown among them.
/// </summary>
internal abstract class ContentPipelineContract
{
    /// <summary>
    ///     Locates every file the site is published from, classifies it and resolves the path and URL it is
    ///     published under.
    /// </summary>
    /// <param name="options">
    ///     Supplies the site configuration the scan is driven by.
    /// </param>
    /// <returns>
    ///     Every file, landing page first, with the files the build has no use for left out.
    /// </returns>
    /// <exception cref="FileNotFoundException">
    ///     The content directory declares no <c>index.md</c>.
    /// </exception>
    public abstract IReadOnlyList<ContentItem> Scan(SiteOptions options);

    /// <summary>
    ///     Reads each located page, renders its Markdown and resolves the files its front matter declares.
    /// </summary>
    /// <remarks>
    ///     Pages are rendered several at a time. When more than one fails, the failure of the first page in
    ///     order surfaces, as it would if they were rendered one after another.
    /// </remarks>
    /// <param name="items">
    ///     Every item a scan located, in the order the pages among them are to be rendered. The other items
    ///     are the files a page's front matter may declare.
    /// </param>
    /// <param name="cancellationToken">
    ///     Token used to cancel the operation.
    /// </param>
    /// <returns>
    ///     Every <see cref="ContentKind.Page"/>, in the order it was given.
    /// </returns>
    /// <exception cref="YamlException">
    ///     A front matter block is not well-formed YAML, or declares a stylesheet or a script the scan did not
    ///     locate.
    /// </exception>
    public abstract IReadOnlyList<ContentPage> Render(IReadOnlyList<ContentItem> items, CancellationToken cancellationToken);
}
