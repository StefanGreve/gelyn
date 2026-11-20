using Gelyn.Model;

namespace Gelyn.Core;

/// <summary>
///     The banner rendered once per build, with no entry marked as the current page, and where in it each entry
///     takes that mark.
/// </summary>
internal sealed record Banner
{
    private const string CurrentMark = " aria-current=\"page\"";

    /// <summary>
    ///     The HTML of the banner, with no entry marked.
    /// </summary>
    public required string Html { get; init; }

    /// <summary>
    ///     The offset into <see cref="Html"/> right after the opening tag of each entry, keyed by the href the
    ///     entry links.
    /// </summary>
    public required IReadOnlyDictionary<string, int> Offsets { get; init; }

    /// <summary>
    ///     Cuts the banner for a page: right after the opening tag of its entry, with the mark as the fragment,
    ///     when the navigation links the page, or after the whole banner, with no fragment, when it does not.
    /// </summary>
    /// <param name="page">
    ///     The page being rendered.
    /// </param>
    /// <returns>
    ///     The banner, cut for the page.
    /// </returns>
    public Slot CutFor(ContentPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        return this.Offsets.TryGetValue(page.Href, out int offset) ? new(this.Html, offset, CurrentMark) : new(this.Html, this.Html.Length, string.Empty);
    }
}
