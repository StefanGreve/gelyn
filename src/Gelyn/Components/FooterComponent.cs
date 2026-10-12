using System;
using System.Globalization;
using System.Net;
using System.Text;

using Gelyn.Core.Internals;
using Gelyn.Model;

namespace Gelyn.Components;

/// <summary>
///     The block rendered at the bottom of every page.
/// </summary>
internal static class FooterComponent
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
        string revised = context.Page.Date is DateOnly date
            ? $" - last revised {date.ToString(DateFormat, CultureInfo.InvariantCulture)}"
            : string.Empty;

        footer.AppendLine("<footer>");
        footer.AppendLine($"<em>Built with {Tool.Name} v{version}{revised}</em>");
        footer.AppendLine("</footer>");

        return footer.ToString();
    }
}
