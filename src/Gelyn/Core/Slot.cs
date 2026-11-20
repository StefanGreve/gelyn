namespace Gelyn.Core;

/// <summary>
///     HTML rendered once per build, cut at the one position where a page writes a fragment of its own.
/// </summary>
/// <param name="Html">
///     The HTML of the block, without the fragment.
/// </param>
/// <param name="Offset">
///     The position in <paramref name="Html"/> at which the fragment goes.
/// </param>
/// <param name="Fragment">
///     The fragment of the page, or an empty string.
/// </param>
internal readonly record struct Slot(string Html, int Offset, string Fragment)
{
    /// <summary>
    ///     The HTML before the fragment.
    /// </summary>
    public ReadOnlySpan<char> Before => this.Html.AsSpan(0, this.Offset);

    /// <summary>
    ///     The HTML after the fragment.
    /// </summary>
    public ReadOnlySpan<char> After => this.Html.AsSpan(this.Offset);
}
