using System.Net.Mime;

namespace Gelyn.Core.Internals;

/// <summary>
///     The file name extensions the tool names, each after the format as <see cref="MediaTypeNames"/> spells it.
/// </summary>
internal static class FileExtensions
{
    /// <summary>
    ///     The extension of an AVIF image.
    /// </summary>
    internal const string Avif = ".avif";

    /// <summary>
    ///     The extension of a CSS stylesheet.
    /// </summary>
    internal const string Css = ".css";

    /// <summary>
    ///     The extension of a GIF image.
    /// </summary>
    internal const string Gif = ".gif";

    /// <summary>
    ///     The extension of a published page.
    /// </summary>
    internal const string Html = ".html";

    /// <summary>
    ///     The extension of an icon.
    /// </summary>
    internal const string Icon = ".ico";

    /// <summary>
    ///     The extension of a JavaScript file.
    /// </summary>
    internal const string JavaScript = ".js";

    /// <summary>
    ///     The extension of a JavaScript module.
    /// </summary>
    internal const string JavaScriptModule = ".mjs";

    /// <summary>
    ///     The extension of a JPEG image, in the long spelling.
    /// </summary>
    internal const string Jpeg = ".jpeg";

    /// <summary>
    ///     The extension of a JPEG image, in the short spelling.
    /// </summary>
    internal const string Jpg = ".jpg";

    /// <summary>
    ///     The extension of a JSON document.
    /// </summary>
    internal const string Json = ".json";

    /// <summary>
    ///     The extension of a Markdown file.
    /// </summary>
    internal const string Markdown = ".md";

    /// <summary>
    ///     The extension of an OpenType font.
    /// </summary>
    internal const string Otf = ".otf";

    /// <summary>
    ///     The extension of a PDF document.
    /// </summary>
    internal const string Pdf = ".pdf";

    /// <summary>
    ///     The extension of a plain text file.
    /// </summary>
    internal const string PlainText = ".txt";

    /// <summary>
    ///     The extension of a PNG image.
    /// </summary>
    internal const string Png = ".png";

    /// <summary>
    ///     The extension of an SVG image.
    /// </summary>
    internal const string Svg = ".svg";

    /// <summary>
    ///     The extension of a TrueType font.
    /// </summary>
    internal const string Ttf = ".ttf";

    /// <summary>
    ///     The extension of a web application manifest.
    /// </summary>
    internal const string WebManifest = ".webmanifest";

    /// <summary>
    ///     The extension of a WebP image.
    /// </summary>
    internal const string Webp = ".webp";

    /// <summary>
    ///     The extension of a WOFF font.
    /// </summary>
    internal const string Woff = ".woff";

    /// <summary>
    ///     The extension of a WOFF2 font.
    /// </summary>
    internal const string Woff2 = ".woff2";

    /// <summary>
    ///     The extension of an XML document.
    /// </summary>
    internal const string Xml = ".xml";
}
