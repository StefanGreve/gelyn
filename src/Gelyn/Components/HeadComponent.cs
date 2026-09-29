using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;

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

        SiteOptions options = context.Options;

        List<string> tags =
        [
            """<meta charset="utf-8">""",
            """<meta name="viewport" content="width=device-width, initial-scale=1">""",
            """<meta name="robots" content="index,follow">""",
            $"""<meta name="generator" content="Gelyn v{WebUtility.HtmlEncode(ToolVersion.Current)}">""",
        ];

        if (!string.IsNullOrWhiteSpace(options.Author))
            tags.Add($"""<meta name="author" content="{WebUtility.HtmlEncode(options.Author)}">""");

        // The page speaks for itself where it can, and borrows the site description where it cannot.
        string? description = context.Page.Description ?? options.Description;

        if (!string.IsNullOrWhiteSpace(description))
            tags.Add($"""<meta name="description" content="{WebUtility.HtmlEncode(description)}">""");

        if (context.Page.Date is DateOnly revised)
        {
            string date = revised.ToString(DateFormat, CultureInfo.InvariantCulture);
            tags.Add($"""<meta name="revised" content="{date}">""");
        }

        if (options.BaseUrl is Uri baseUrl)
        {
            string canonical = new Uri(baseUrl, context.Page.Href).AbsoluteUri;
            tags.Add($"""<link rel="canonical" href="{WebUtility.HtmlEncode(canonical)}">""");
        }

        tags.Add($"<title>{WebUtility.HtmlEncode(context.Page.Title)}</title>");

        return string.Join(Environment.NewLine, tags);
    }
}
