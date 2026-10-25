using System.IO.Abstractions;
using System.Net;
using System.Net.Mime;

using Gelyn.Core.Internals;
using Gelyn.Model;

namespace Gelyn.Services;

/// <summary>
///     Decides which file in the output directory answers a given request path.
/// </summary>
/// <remarks>
///     Resolution follows the conventions of the targets a generated site is deployed to, so that a preview
///     disagrees with the deployed site in as few places as it can:
///     <list type="number">
///         <item>
///             <description>
///                 A directory, with or without a trailing slash, resolves to the <c>index.html</c> it holds.
///             </description>
///         </item>
///         <item>
///             <description>
///                 An extensionless path resolves to the <c>.html</c> file of that name, which is what both
///                 GitHub Pages and Cloudflare Pages serve.
///             </description>
///         </item>
///         <item>
///             <description>
///                 Anything else resolves to <c>404.html</c> in the output root, or to a bodyless 404 when
///                 the site declares no such page.
///             </description>
///         </item>
///     </list>
///     The output directory arrives per call and is never held, because it belongs to one serving session
///     instead of to the resolver. It is resolved again on each request, so that a caller cannot weaken the
///     containment test by passing a path that was never normalized.
/// </remarks>
internal sealed class StaticFileResolver
{
    private const string IndexFileName = "index.html";
    private const string NotFoundFileName = "404.html";
    private const string HtmlExtension = ".html";

    // Appended to the media types that register a charset parameter. The generator writes UTF-8 throughout,
    // so declaring it spares the browser the sniffing that a missing charset leaves it to.
    private const string Charset = "; charset=utf-8";

    private const string HtmlContentType = MediaTypeNames.Text.Html + Charset;
    private const string PlainTextContentType = MediaTypeNames.Text.Plain + Charset;
    private const string CssContentType = MediaTypeNames.Text.Css + Charset;
    private const string JavaScriptContentType = MediaTypeNames.Text.JavaScript + Charset;
    private const string XmlContentType = MediaTypeNames.Application.Xml + Charset;
    private const string SvgContentType = MediaTypeNames.Image.Svg + Charset;

    private readonly IFileSystem _fileSystem;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StaticFileResolver"/> class.
    /// </summary>
    /// <param name="fileSystem">
    ///     The file system to work against.
    /// </param>
    public StaticFileResolver(IFileSystem fileSystem)
    {
        this._fileSystem = fileSystem;
    }

    /// <summary>
    ///     Resolves one request path against the output directory.
    /// </summary>
    /// <param name="outputDirectory">
    ///     The directory the generated site was written to, which is the only directory a request can reach.
    /// </param>
    /// <param name="requestPath">
    ///     The path of the requested URL, percent-encoded as the client sent it.
    /// </param>
    /// <returns>
    ///     The file that answers the request together with the status and media type to answer under, or a
    ///     404 when nothing in the output directory does.
    /// </returns>
    public ServedFile Resolve(string outputDirectory, string requestPath)
    {
        ArgumentNullException.ThrowIfNull(outputDirectory);
        ArgumentNullException.ThrowIfNull(requestPath);

        string root = PathBoundary.Resolve(outputDirectory);

        return this.TryLocate(root, requestPath, out string? located)
            ? new ServedFile
            {
                Path = located,
                ContentType = Classify(this._fileSystem.Path.GetExtension(located)),
                StatusCode = HttpStatusCode.OK,
            }
            : this.NotFound(root);
    }

    #region Helpers

    /// <summary>
    ///     Locates the file one request path names, provided it is a file inside the output directory.
    /// </summary>
    /// <param name="root">
    ///     The resolved output directory.
    /// </param>
    /// <param name="requestPath">
    ///     The path of the requested URL, percent-encoded as the client sent it.
    /// </param>
    /// <param name="path">
    ///     Receives the absolute path of the located file, or <see langword="null"/> when there is none.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> when a file was located, <see langword="false"/> otherwise.
    /// </returns>
    private bool TryLocate(string root, string requestPath, [NotNullWhen(true)] out string? path)
    {
        path = null;

        // Decoded before the resolve, so that an encoded separator is collapsed instead of surviving into a
        // file name. The leading one goes first, or Path.Combine reads the whole request path as rooted.
        string relative = Uri.UnescapeDataString(requestPath).TrimStart('/');
        string candidate;

        try
        {
            candidate = PathBoundary.Resolve(root, relative);
        }
        catch (ArgumentException)
        {
            return false;
        }

        // One test over the resolved path covers `..`, a rooted path and an encoded separator alike, and it
        // runs before any file is touched.
        if (!PathBoundary.Contains(root, candidate))
            return false;

        IPath paths = this._fileSystem.Path;

        if (this._fileSystem.Directory.Exists(candidate))
            return this.TryUse(paths.Combine(candidate, IndexFileName), out path);

        if (this.TryUse(candidate, out path))
            return true;

        return paths.GetExtension(candidate).Length == 0
            && this.TryUse(candidate + HtmlExtension, out path);
    }

    private bool TryUse(string candidate, [NotNullWhen(true)] out string? path)
    {
        path = this._fileSystem.File.Exists(candidate) ? candidate : null;

        return path is not null;
    }

    // Looked for in the output root alone, because that is where a site shipping one page puts it and where
    // both deploy targets fall back to when no page sits closer to the request.
    private ServedFile NotFound(string root)
    {
        string page = this._fileSystem.Path.Combine(root, NotFoundFileName);
        bool declared = this._fileSystem.File.Exists(page);

        return new ServedFile
        {
            Path = declared ? page : null,
            ContentType = declared ? HtmlContentType : PlainTextContentType,
            StatusCode = HttpStatusCode.NotFound,
        };
    }

    // A switch over string literals compares ordinally, so matching an extension that may carry any spelling
    // needs the when clauses that ContentPipeline.Classify uses for the same reason.
    private static string Classify(string extension) => extension switch
    {
        var value when value.Equals(HtmlExtension, StringComparison.OrdinalIgnoreCase) => HtmlContentType,
        var value when value.Equals(".css", StringComparison.OrdinalIgnoreCase) => CssContentType,
        var value when value.Equals(".js", StringComparison.OrdinalIgnoreCase) => JavaScriptContentType,
        var value when value.Equals(".mjs", StringComparison.OrdinalIgnoreCase) => JavaScriptContentType,
        var value when value.Equals(".txt", StringComparison.OrdinalIgnoreCase) => PlainTextContentType,
        var value when value.Equals(".xml", StringComparison.OrdinalIgnoreCase) => XmlContentType,
        var value when value.Equals(".svg", StringComparison.OrdinalIgnoreCase) => SvgContentType,
        var value when value.Equals(".json", StringComparison.OrdinalIgnoreCase) => MediaTypeNames.Application.Json,
        var value when value.Equals(".webmanifest", StringComparison.OrdinalIgnoreCase) => MediaTypeNames.Application.Manifest,
        var value when value.Equals(".ico", StringComparison.OrdinalIgnoreCase) => MediaTypeNames.Image.Icon,
        var value when value.Equals(".png", StringComparison.OrdinalIgnoreCase) => MediaTypeNames.Image.Png,
        var value when value.Equals(".jpg", StringComparison.OrdinalIgnoreCase) => MediaTypeNames.Image.Jpeg,
        var value when value.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) => MediaTypeNames.Image.Jpeg,
        var value when value.Equals(".gif", StringComparison.OrdinalIgnoreCase) => MediaTypeNames.Image.Gif,
        var value when value.Equals(".webp", StringComparison.OrdinalIgnoreCase) => MediaTypeNames.Image.Webp,
        var value when value.Equals(".avif", StringComparison.OrdinalIgnoreCase) => MediaTypeNames.Image.Avif,
        var value when value.Equals(".woff", StringComparison.OrdinalIgnoreCase) => MediaTypeNames.Font.Woff,
        var value when value.Equals(".woff2", StringComparison.OrdinalIgnoreCase) => MediaTypeNames.Font.Woff2,
        var value when value.Equals(".ttf", StringComparison.OrdinalIgnoreCase) => MediaTypeNames.Font.Ttf,
        var value when value.Equals(".otf", StringComparison.OrdinalIgnoreCase) => MediaTypeNames.Font.Otf,
        var value when value.Equals(".pdf", StringComparison.OrdinalIgnoreCase) => MediaTypeNames.Application.Pdf,
        _ => MediaTypeNames.Application.Octet,
    };

    #endregion
}
