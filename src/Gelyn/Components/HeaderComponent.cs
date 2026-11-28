using System.Net;

using Gelyn.Core;
using Gelyn.Core.Internals.Primitives;
using Gelyn.Model;
using Gelyn.Model.Options;

namespace Gelyn.Components;

/// <summary>
///     The banner rendered at the top of every page, including the site navigation.
/// </summary>
internal static class HeaderComponent
{
    /// <summary>
    ///     Renders the banner once for the build, with no entry marked, and notes where each entry takes its mark.
    /// </summary>
    /// <param name="options">
    ///     The configuration snapshot the build is rendered against, which names the site.
    /// </param>
    /// <param name="navigation">
    ///     The pages the site navigation links, landing page first.
    /// </param>
    /// <returns>
    ///     The banner.
    /// </returns>
    public static Banner Render(SiteOptions options, IReadOnlyList<ContentPage> navigation)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(navigation);

        using var html = new PageBuffer();
        var offsets = new Dictionary<string, int>(navigation.Count, StringComparer.Ordinal);

        html.Append("<header><nav><ul>"u8);

        for (int index = 0; index < navigation.Count; index++)
        {
            ContentPage page = navigation[index];
            string title = index == 0 ? options.Title : page.Title;

            html.Append("<li><a"u8);
            offsets.Add(page.Href, html.Length);
            html.Append($""" href="{WebUtility.HtmlEncode(page.Href)}">{WebUtility.HtmlEncode(title)}</a></li>""");
        }

        html.Append("</ul></nav></header>"u8);

        return new Banner { Bytes = html.WrittenSpan.ToArray(), Offsets = offsets };
    }
}
