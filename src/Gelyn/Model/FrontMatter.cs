using System;

namespace Gelyn.Model;

/// <summary>
///     Front matter declared in the block at the top of a content file.
/// </summary>
/// <remarks>
///     A property is <see langword="null"/> only when the block declares no value for it. A value declared as an
///     empty string is kept as one, so the two cases stay distinguishable. The properties carry a setter rather
///     than an <c>init</c> accessor because the generated deserialization code assigns them from an ordinary
///     statement, which C# permits only inside an object initializer or a constructor.
/// </remarks>
public sealed record FrontMatter
{
    /// <summary>
    ///     The page title, or <see langword="null"/> when the block omits it.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    ///     The publication date, or <see langword="null"/> when the block omits it.
    /// </summary>
    public DateOnly? Date { get; set; }

    /// <summary>
    ///     The page description, or <see langword="null"/> when the block omits it.
    /// </summary>
    public string? Description { get; set; }
}
