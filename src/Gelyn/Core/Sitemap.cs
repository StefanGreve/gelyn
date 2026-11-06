using System.Text;
using System.Xml;

using Gelyn.Core.Internals;
using Gelyn.Model;
using Gelyn.Model.Options;

namespace Gelyn.Core;

/// <summary>
///     The inventory of pages published beside the generated site, as a Sitemap 0.9 document.
/// </summary>
/// <remarks>
///     Built with <see cref="XmlWriter"/> on the grounds <see cref="AtomFeed"/> records, and assembled in
///     memory and returned as bytes for the same reason: it keeps the render a pure function and leaves the
///     single write to the caller. The two documents have no element in common, so they share no code.
/// </remarks>
/// <seealso href="https://www.sitemaps.org/protocol.html">Sitemaps XML format</seealso>
internal static class Sitemap
{
    /// <summary>
    ///     The name the sitemap is published under, relative to the output root.
    /// </summary>
    /// <remarks>
    ///     The protocol scopes a sitemap to the directory it sits in, so the output root is the one location
    ///     from which the document can claim every page it lists.
    /// </remarks>
    internal const string FileName = "sitemap.xml";

    // Every element of the document belongs to this namespace, so each one is written with it. A child
    // written without would land in no namespace and carry an xmlns="" of its own to say so.
    private const string Namespace = "http://www.sitemaps.org/schemas/sitemap/0.9";

    /// <summary>
    ///     Renders the sitemap.
    /// </summary>
    /// <param name="options">
    ///     The site configuration, whose base URL every location is resolved against.
    /// </param>
    /// <param name="pages">
    ///     Every page the site publishes, listed in the order it is given them except for the not-found page,
    ///     which has no address of its own to list. The protocol assigns the order of the locations no meaning,
    ///     so none is imposed here.
    /// </param>
    /// <returns>
    ///     The UTF-8 bytes of the document, or an empty array when the configuration declares no base URL,
    ///     which is the only origin a location can be resolved against.
    /// </returns>
    public static byte[] Render(SiteOptions options, IReadOnlyList<ContentPage> pages)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(pages);

        if (options.BaseUrl is not Uri baseUrl) return [];

        Uri root = UrlUtils.AsDirectory(baseUrl);

        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = true,
        };

        using var buffer = new MemoryStream();

        using (var writer = XmlWriter.Create(buffer, settings))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("urlset", Namespace);

            foreach (ContentPage page in pages.Where(static page => !page.IsNotFoundPage))
                writer.WriteLocation(root, page);

            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        return buffer.ToArray();
    }

    #region Helpers

    /// <summary>
    ///     Writes the entry for one page.
    /// </summary>
    /// <param name="writer">
    ///     The writer the document is being built with.
    /// </param>
    /// <param name="root">
    ///     The base URL as a directory, which the location is resolved against.
    /// </param>
    /// <param name="page">
    ///     The page the entry stands for, which supplies the location and the date it carries.
    /// </param>
    private static void WriteLocation(this XmlWriter writer, Uri root, ContentPage page)
    {
        writer.WriteStartElement("url", Namespace);

        // The href already carries the path of the base URL, so it resolves against the origin alone.
        writer.WriteElementString("loc", Namespace, new Uri(root, page.Href).AbsoluteUri);

        // Omitted for a page declaring neither date, a wrong value being worse than none to a crawler.
        if (page.Revised is DateOnly revised)
            writer.WriteElementString("lastmod", Namespace, revised.ToString(DateFormat.Machine, Tool.Culture));

        writer.WriteEndElement();
    }

    #endregion
}
