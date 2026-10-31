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

        // Resolving a relative reference against a base URL whose path names no directory drops its last
        // segment, so the trailing slash is what keeps a site served from below a domain root addressable.
        var root = new Uri(StringUtils.EnsureEndsOn(baseUrl.AbsoluteUri, '/'));

        XmlWriterSettings settings = new()
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = true,
        };

        using MemoryStream buffer = new();

        // Closed before the bytes are read, because the writer holds the tail of the document until it is.
        using (XmlWriter writer = XmlWriter.Create(buffer, settings))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("feed", Namespace);

            WriteMetadata(writer, options, root, entries[0]);

            foreach (ContentPage page in entries)
                WriteEntry(writer, options, root, page);

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
    /// <param name="newest">
    ///     The most recent entry, which is what dates the feed.
    /// </param>
    private static void WriteMetadata(XmlWriter writer, SiteOptions options, Uri root, ContentPage newest)
    {
        writer.WriteElementString("id", Namespace, root.AbsoluteUri);
        writer.WriteElementString("title", Namespace, options.Title);

        if (!string.IsNullOrWhiteSpace(options.Description))
            writer.WriteElementString("subtitle", Namespace, options.Description);

        // Dated from the content and never from the clock, so that a build of unchanged pages is byte for
        // byte the build before it.
        writer.WriteElementString("updated", Namespace, ToInstant(newest.Date));

        WriteLink(writer, "self", new Uri(root, FileName).AbsoluteUri, ContentType);
        WriteLink(writer, "alternate", root.AbsoluteUri, MediaTypeNames.Text.Html);

        // Required of a feed whose entries declare no author of their own, which is every entry here, since
        // a page carries no author of its own. A site is the author of what it publishes.
        writer.WriteStartElement("author", Namespace);
        writer.WriteElementString("name", Namespace, options.Author ?? options.Title);
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
    ///     The site configuration, which supplies the description a page declaring none falls back to.
    /// </param>
    /// <param name="root">
    ///     The base URL as a directory, which the entry's links are resolved against.
    /// </param>
    /// <param name="page">
    ///     The page the entry stands for.
    /// </param>
    private static void WriteEntry(XmlWriter writer, SiteOptions options, Uri root, ContentPage page)
    {
        // The href already carries the path of the base URL, so it resolves against the origin alone.
        string address = new Uri(root, page.Href).AbsoluteUri;
        string instant = ToInstant(page.Date);

        writer.WriteStartElement("entry", Namespace);

        writer.WriteElementString("id", Namespace, address);
        writer.WriteElementString("title", Namespace, page.Title);

        // One date answers both, a page declaring when it was published and nothing about later revisions.
        writer.WriteElementString("updated", Namespace, instant);
        writer.WriteElementString("published", Namespace, instant);

        // Required of an entry carrying no atom:content, which is every entry: the rendered body still holds
        // its unexpanded markers at this point, and its links are site-relative where a reader needs absolute.
        WriteLink(writer, "alternate", address, MediaTypeNames.Text.Html);

        // An empty page description wins over the site's and leaves the entry with no summary, which is how
        // HeadComponent reads the same pair.
        string? summary = page.Description ?? options.Description;

        if (!string.IsNullOrWhiteSpace(summary))
        {
            writer.WriteStartElement("summary", Namespace);
            writer.WriteAttributeString("type", TextType);
            writer.WriteString(summary);
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }

    private static void WriteLink(XmlWriter writer, string relation, string address, string type)
    {
        writer.WriteStartElement("link", Namespace);
        writer.WriteAttributeString("rel", relation);
        writer.WriteAttributeString("type", type);
        writer.WriteAttributeString("href", address);
        writer.WriteEndElement();
    }

    // Every entry was filtered and sorted on its date, so the throw states an invariant and is never reached.
    private static string ToInstant(DateOnly? date)
    {
        DateOnly value = date ?? throw new InvalidOperationException("A feed entry always declares a date.");

        return value
            .ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
            .ToString(DateFormat.Instant, Tool.Culture);
    }

    #endregion
}
