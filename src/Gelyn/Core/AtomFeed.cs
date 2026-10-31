using System.Net.Mime;
using System.Text;
using System.Xml;

using Gelyn.Core.Internals;
using Gelyn.Model;
using Gelyn.Model.Options;

namespace Gelyn.Core;

/// <summary>
///     The syndication feed published beside the generated pages, as an Atom 1.0 document.
/// </summary>
/// <remarks>
///     Built with <see cref="XmlWriter"/> on two grounds: <c>System.ServiceModel.Syndication</c> is a poor
///     fit for Native AOT, and the writer escapes every value it is handed, where the HTML components have to
///     encode theirs. The document is assembled in memory and returned as bytes, which keeps this a pure
///     function and leaves the single write to the caller.
/// </remarks>
/// <seealso href="https://www.rfc-editor.org/rfc/rfc4287.html">RFC 4287, The Atom Syndication Format</seealso>
internal static class AtomFeed
{
    /// <summary>
    ///     The name the feed is published under, relative to the output root.
    /// </summary>
    internal const string FileName = "feed.xml";

    // Every element of the document belongs to this namespace, so each one is written with it. A child
    // written without would land in no namespace and carry an xmlns="" of its own to say so.
    private const string Namespace = "http://www.w3.org/2005/Atom";

    // RFC 4287 registers this media type, and System.Net.Mime declares no constant for it.
    private const string ContentType = "application/atom+xml";

    private const string TextType = "text";

    // Stood in for the author of a site that names none, RFC 4287 requiring the feed to name one.
    private const string AnonymousAuthor = "anonymous";

    /// <summary>
    ///     Renders the feed.
    /// </summary>
    /// <param name="options">
    ///     The site configuration, whose base URL every link is resolved against.
    /// </param>
    /// <param name="pages">
    ///     Every page the site is built from, in any order.
    /// </param>
    /// <returns>
    ///     The UTF-8 bytes of the document, or an empty array when no feed can be written, which is the case
    ///     both when the configuration declares no base URL and when no page qualifies as an entry.
    /// </returns>
    public static byte[] Render(SiteOptions options, IReadOnlyList<ContentPage> pages)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(pages);

        if (options.BaseUrl is not Uri baseUrl) return [];

        ContentPage[] entries = [.. pages
            .Where(static page => page.InFeed && page.Date is not null)
            .OrderByDescending(static page => page.Date)
            .ThenBy(static page => page.Title, StringComparer.Ordinal)];

        if (entries.Length == 0) return [];

        // Without the trailing slash, resolving against it would drop the last segment of the path.
        var root = new Uri(StringUtils.EnsureEndsOn(baseUrl.AbsoluteUri, '/'));

        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = true,
        };

        using var buffer = new MemoryStream();

        using (var writer = XmlWriter.Create(buffer, settings))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("feed", Namespace);

            // The newest revision, which is not the first entry: that one is the newest publication.
            writer.WriteMetadata(options, root, entries.Max(static page => page.Revised));

            foreach (ContentPage page in entries)
                writer.WriteEntry(options, root, page);

            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        return buffer.ToArray();
    }

    #region Helpers

    /// <summary>
    ///     Writes the elements that describe the feed itself, ahead of its entries.
    /// </summary>
    /// <param name="writer">
    ///     The writer the document is being built with.
    /// </param>
    /// <param name="options">
    ///     The site configuration the feed describes.
    /// </param>
    /// <param name="root">
    ///     The base URL as a directory, which every link is resolved against.
    /// </param>
    /// <param name="updated">
    ///     The date the feed itself carries, which is the most recent revision among its entries.
    /// </param>
    private static void WriteMetadata(this XmlWriter writer, SiteOptions options, Uri root, DateOnly? updated)
    {
        writer.WriteElementString("id", Namespace, root.AbsoluteUri);
        writer.WriteElementString("title", Namespace, options.Title);

        if (!string.IsNullOrWhiteSpace(options.Description))
            writer.WriteElementString("subtitle", Namespace, options.Description);

        writer.WriteElementString("updated", Namespace, ToInstant(updated));

        writer.WriteLink("self", new Uri(root, FileName).AbsoluteUri, ContentType);
        writer.WriteLink("alternate", root.AbsoluteUri, MediaTypeNames.Text.Html);

        writer.WriteStartElement("author", Namespace);
        writer.WriteElementString("name", Namespace, options.Author ?? AnonymousAuthor);
        writer.WriteEndElement();

        writer.WriteStartElement("generator", Namespace);
        writer.WriteAttributeString("version", Tool.Version);
        writer.WriteString(Tool.Name);
        writer.WriteEndElement();
    }

    /// <summary>
    ///     Writes one entry.
    /// </summary>
    /// <param name="writer">
    ///     The writer the document is being built with.
    /// </param>
    /// <param name="options">
    ///     The site configuration, which supplies the summary.
    /// </param>
    /// <param name="root">
    ///     The base URL as a directory, which the entry's links are resolved against.
    /// </param>
    /// <param name="page">
    ///     The page the entry stands for.
    /// </param>
    private static void WriteEntry(this XmlWriter writer, SiteOptions options, Uri root, ContentPage page)
    {
        // The href already carries the path of the base URL, so it resolves against the origin alone.
        string address = new Uri(root, page.Href).AbsoluteUri;

        writer.WriteStartElement("entry", Namespace);

        writer.WriteElementString("id", Namespace, address);
        writer.WriteElementString("title", Namespace, page.Title);
        writer.WriteElementString("updated", Namespace, ToInstant(page.Revised));
        writer.WriteElementString("published", Namespace, ToInstant(page.Date));

        writer.WriteLink("alternate", address, MediaTypeNames.Text.Html);

        string? summary = options.Description;

        if (!string.IsNullOrWhiteSpace(summary))
        {
            writer.WriteStartElement("summary", Namespace);
            writer.WriteAttributeString("type", TextType);
            writer.WriteString(summary);
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }

    private static void WriteLink(this XmlWriter writer, string relation, string address, string type)
    {
        writer.WriteStartElement("link", Namespace);
        writer.WriteAttributeString("rel", relation);
        writer.WriteAttributeString("type", type);
        writer.WriteAttributeString("href", address);
        writer.WriteEndElement();
    }

    private static string ToInstant(DateOnly? date)
    {
        DateOnly value = date ?? throw new InvalidOperationException("A feed entry always declares a date.");

        return value
            .ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
            .ToString(DateFormat.Instant, Tool.Culture);
    }

    #endregion
}
