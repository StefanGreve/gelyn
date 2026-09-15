using System;

namespace Gelyn.Model;

/// <summary>
///     Metadata declared in the block at the top of a content file.
/// </summary>
internal sealed record MetaData
{
    /// <summary>
    ///     Metadata with no values set.
    /// </summary>
    internal static MetaData Empty { get; } = new();

    /// <summary>
    ///     The page title, or <see langword="null"/> when the block omits it.
    /// </summary>
    internal string? Title { get; init; }

    /// <summary>
    ///     The publication date, or <see langword="null"/> when the block omits it.
    /// </summary>
    internal DateOnly? Date { get; init; }
}
