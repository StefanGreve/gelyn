using Gelyn.Core.Internals;
using Gelyn.Model;

namespace Gelyn.Core;

/// <summary>
///     The footer rendered once per build, with no revision fragment, and where in it a page's fragment goes.
/// </summary>
internal sealed record Footer
{
    /// <summary>
    ///     The UTF-8 bytes of the footer, with no revision fragment.
    /// </summary>
    public required ReadOnlyMemory<byte> Bytes { get; init; }

    /// <summary>
    ///     The offset into <see cref="Bytes"/> at the end of the colophon, where the revision fragment goes.
    /// </summary>
    public required int Offset { get; init; }

    /// <summary>
    ///     Cuts the footer for a page: at the end of the colophon, with the revision fragment of a revised page as
    ///     the fragment, and with none for any other page.
    /// </summary>
    /// <param name="page">
    ///     The page being rendered.
    /// </param>
    /// <returns>
    ///     The footer, cut for the page.
    /// </returns>
    public Slot CutFor(ContentPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        return new(this.Bytes, this.Offset, page.Revised is DateOnly revised ? RenderRevision(revised) : string.Empty);
    }

    #region Helpers

    private static string RenderRevision(DateOnly revised) =>
        $""" - last revised <time datetime="{revised.ToString(DateFormat.Machine, Tool.Culture)}">{revised.ToString(DateFormat.Display, Tool.Culture)}</time>""";

    #endregion
}
