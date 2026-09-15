using System;

namespace Gelyn.Model;

/// <summary>
///     Metadata declared in the block at the top of a content file.
/// </summary>
/// <param name="Title">
///     The page title, or <see langword="null"/> when the block omits it.
/// </param>
/// <param name="Date">
///     The publication date, or <see langword="null"/> when the block omits it.
/// </param>
internal sealed record MetaData(string? Title, DateOnly? Date)
{
    /// <summary>
    ///     Metadata with no values set.
    /// </summary>
    internal static MetaData Empty { get; } = new(null, null);
}
