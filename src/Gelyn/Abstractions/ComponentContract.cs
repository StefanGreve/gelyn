namespace Gelyn.Abstractions;

/// <summary>
///     A reusable markup fragment shared by every generated page.
/// </summary>
internal abstract class ComponentContract
{
    /// <summary>
    ///     Renders the fragment.
    /// </summary>
    /// <returns>
    ///     The HTML of the fragment.
    /// </returns>
    public abstract string Render();
}
