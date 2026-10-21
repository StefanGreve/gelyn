using System.Net;

using Gelyn.Model;

namespace Gelyn.Components;

/// <summary>
///     The banner rendered at the top of every page, including the site navigation.
/// </summary>
internal static class HeaderComponent
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

        return $"""
            <header>
                <nav>
                    <ul>
                        {string.Join(Environment.NewLine, navItems)}
                    </ul>
                </nav>
            </header>
        """;
    }
}
