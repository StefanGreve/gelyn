using System;
using System.Net;

using Gelyn.Model;

namespace Gelyn.Components;

/// <summary>
///     The block rendered at the bottom of every page.
/// </summary>
public static class FooterComponent
{
    /// <summary>
    ///     Renders the block.
    /// </summary>
    /// <param name="context">
    ///     Supplies the site title.
    /// </param>
    /// <returns>
    ///     The HTML of the fragment.
    /// </returns>
    public static string Render(RenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return $"""
            <footer>
              <p>{WebUtility.HtmlEncode(context.Options.Title)}</p>
            </footer>
        """;
    }
}
