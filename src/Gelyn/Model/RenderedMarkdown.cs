namespace Gelyn.Model;

/// <summary>
///     The result of rendering a content file: its front matter and the HTML body it produced.
/// </summary>
internal sealed record RenderedMarkdown
{
    /// <summary>
    ///     Front matter parsed from the block at the top of the file, or <see langword="null"/> when the file
    ///     declares none.
    /// </summary>
    public required FrontMatter? FrontMatter { get; init; }

    /// <summary>
    ///     The keys the front matter declares that no member reads, in the order declared.
    /// </summary>
    public required IReadOnlyList<UnknownKey> UnknownKeys { get; init; }

    /// <summary>
    ///     The values the front matter declares that the member meant for them rejects, in the order declared.
    /// </summary>
    public required IReadOnlyList<InvalidValue> InvalidValues { get; init; }

    /// <summary>
    ///     The rendered HTML body as UTF-8, excluding the front matter block.
    /// </summary>
    public required ReadOnlyMemory<byte> Body { get; init; }
}
