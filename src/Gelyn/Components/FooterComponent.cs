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
    private const string MachineDateFormat = "yyyy-MM-dd";

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
        string revised = string.Empty;

        if (context.Page.Date is DateOnly date)
        {
            // The attribute is a machine format, so it stays invariant even if the displayed date is localized.
            string machine = date.ToString(MachineDateFormat, CultureInfo.InvariantCulture);
            revised = $""" - last revised <time datetime="{machine}">{date.ToString(DateFormat, Tool.Culture)}</time>""";
        }

        footer.AppendLine("<footer>");
        footer.AppendLine($"Built with {Tool.Name} v{version}{revised}");
        footer.AppendLine("</footer>");

        return footer.ToString();
    }
}
