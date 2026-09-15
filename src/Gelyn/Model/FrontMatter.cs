using System;

namespace Gelyn.Model;

/// <summary>
///     Front matter declared in the block at the top of a content file.
/// </summary>
public sealed record FrontMatter
{
    /// <summary>
    ///     Front matter with no values set.
    /// </summary>
    public static FrontMatter Empty { get; } = new();

    /// <summary>
    ///     The page title, or <see langword="null"/> when the block omits it.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    ///     The publication date, or <see langword="null"/> when the block omits it.
    /// </summary>
    public DateOnly? Date { get; init; }
}
