using System;
using System.Linq;
using System.Net;

using Gelyn.Model;

namespace Gelyn.Components;

/// <summary>
///     The banner rendered at the top of every page, including the site navigation.
/// </summary>
public static class HeaderComponent
{
    /// <summary>
    ///     Renders the banner.
    /// </summary>
    /// <param name="context">
    ///     Supplies the site title and every page to link.
    /// </param>
    /// <returns>
    ///     The HTML of the fragment.
    /// </returns>
    public static string Render(RenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var navItems = context.Pages.Select(static page =>
            $"""<a href="{WebUtility.HtmlEncode(page.Href)}">{WebUtility.HtmlEncode(page.Title)}</a>"""
        );

        // Pages is ordered landing page first
        string home = WebUtility.HtmlEncode(context.Pages[0].Href);

        return $"""
            <header>
              <a href="{home}">{WebUtility.HtmlEncode(context.Options.Title)}</a>
              <nav>
                {string.Join(Environment.NewLine, navItems)}
              </nav>
            </header>
        """;
    }
}
