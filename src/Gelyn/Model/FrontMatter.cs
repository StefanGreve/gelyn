using System;

namespace Gelyn.Model;

/// <summary>
///     Front matter declared in the block at the top of a content file.
/// </summary>
public sealed record FrontMatter
{
    /// <summary>
    ///     The page title, or <see langword="null"/> when the block omits it.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    ///     The publication date, or <see langword="null"/> when the block omits it.
    /// </summary>
    public DateOnly? Date { get; init; }

    /// <summary>
    ///     The page description, or <see langword="null"/> when the block omits it.
    /// </summary>
    public string? Description { get; init; }
}
