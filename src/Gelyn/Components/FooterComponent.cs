using System;
using System.Globalization;
using System.Net;

using Gelyn.Internals;
using Gelyn.Model;

namespace Gelyn.Components;

/// <summary>
///     The block rendered at the bottom of every page.
/// </summary>
public static class FooterComponent
{
    private const string DateFormat = "MMM d, yyyy";

    /// <summary>
    ///     Renders the block.
    /// </summary>
    /// <param name="context">
    ///     Supplies the date the build started.
    /// </param>
    /// <returns>
    ///     The HTML of the fragment.
    /// </returns>
    public static string Render(RenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string version = WebUtility.HtmlEncode(ToolVersion.Current);
        string generated = context.GeneratedAt.ToString(DateFormat, CultureInfo.InvariantCulture);

        return $"""
            <footer>
                <em>Built with Gelyn v{version} on {generated}</em>
            </footer>
        """;
    }
}
