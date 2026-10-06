using System;
using System.Globalization;
using System.Net;
using System.Text;

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
    ///     The context the page is rendered against.
    /// </param>
    /// <returns>
    ///     The HTML of the fragment.
    /// </returns>
    public static string Render(RenderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var footer = new StringBuilder();

        string version = WebUtility.HtmlEncode(Tool.Version);
        string generated = context.GeneratedAt.ToString(DateFormat, CultureInfo.InvariantCulture);

        footer.AppendLine("<footer>");
        footer.AppendLine($"<em>Built with {Tool.Name} v{version} on {generated}</em>");
        footer.AppendLine("</footer>");

        return footer.ToString();
    }
}
