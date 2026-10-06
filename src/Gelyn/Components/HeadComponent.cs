using System;
using System.Globalization;
using System.Net;
using System.Text;

using Gelyn.Internals;
using Gelyn.Model;
using Gelyn.Model.Options;

namespace Gelyn.Components;

/// <summary>
///     The document metadata written to the head of every page.
/// </summary>
public static class HeadComponent
{
    private const string DateFormat = "yyyy-MM-dd";

    /// <summary>
    ///     Renders the metadata.
    /// </summary>
    /// <param name="context">
    ///     The context the page is rendered against.
    /// </param>
    /// <returns>
    ///     The HTML of the fragment, without the enclosing <c>head</c> element.
    /// </returns>
    public static string Render(RenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        StringBuilder head = new();
        SiteOptions options = context.Options;

        head.AppendLine("""<meta charset="utf-8">""");
        head.AppendLine("""<meta name="viewport" content="width=device-width, initial-scale=1">""");
        head.AppendLine("""<meta name="robots" content="index,follow">""");
        head.AppendLine($"""<meta name="generator" content="{Tool.Name} v{WebUtility.HtmlEncode(Tool.Version)}">""");

        if (!string.IsNullOrWhiteSpace(options.Author))
            head.AppendLine($"""<meta name="author" content="{WebUtility.HtmlEncode(options.Author)}">""");

        // The page speaks for itself where it can, and borrows the site description where it cannot.
        string? description = context.Page.Description ?? options.Description;

        if (!string.IsNullOrWhiteSpace(description))
            head.AppendLine($"""<meta name="description" content="{WebUtility.HtmlEncode(description)}">""");

        if (context.Page.Date is DateOnly revised)
        {
            string date = revised.ToString(DateFormat, CultureInfo.InvariantCulture);
            head.AppendLine($"""<meta name="revised" content="{date}">""");
        }

        if (options.BaseUrl is Uri baseUrl)
        {
            string canonical = new Uri(baseUrl, context.Page.Href).AbsoluteUri;
            head.AppendLine($"""<link rel="canonical" href="{WebUtility.HtmlEncode(canonical)}">""");
        }

        head.AppendLine($"<title>{WebUtility.HtmlEncode(context.Page.Title)}</title>");

        return head.ToString();
    }
}
