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
    ///     The context the page is rendered against.
    /// </param>
    /// <returns>
    ///     The HTML of the fragment.
    /// </returns>
    public static string Render(RenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var navItems = context.Navigation.Select(page =>
        {
            bool isCurrent = string.Equals(context.Page.Href, page.Href, StringComparison.Ordinal);
            string current = isCurrent ? "aria-current=\"page\"" : string.Empty;

            return $"""
                <li>
                    <a {current} href="{WebUtility.HtmlEncode(page.Href)}">{WebUtility.HtmlEncode(page.Title)}</a>
                </li>
            """;
        });

        // Navigation is ordered landing page first
        string home = WebUtility.HtmlEncode(context.Navigation[0].Href);

        return $"""
            <header>
                <a href="{home}">{WebUtility.HtmlEncode(context.Options.Title)}</a>
                <nav>
                    <ul>
                        {string.Join(Environment.NewLine, navItems)}
                    </ul>
                </nav>
            </header>
        """;
    }
}
