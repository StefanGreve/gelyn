namespace Gelyn.Core.Yaml;

/// <summary>
///     The kinds of value that a key or an entry of a sequence holds.
/// </summary>
internal enum YamlNodeKind
{
    /// <summary>
    ///     An empty value, or a plain spelling of null.
    /// </summary>
    Null,

    /// <summary>
    ///     A scalar in any style, whose text the parser holds.
    /// </summary>
    Scalar,

    /// <summary>
    ///     A sequence in block or in flow style.
    /// </summary>
    Sequence,

    /// <summary>
    ///     A mapping in block or in flow style.
    /// </summary>
    Mapping,
}
