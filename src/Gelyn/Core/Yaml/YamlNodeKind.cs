namespace Gelyn.Core.Yaml;

/// <summary>
///     The kinds of value that a key or an entry of a list holds.
/// </summary>
internal enum YamlNodeKind
{
    /// <summary>
    ///     An empty value, or a plain spelling of null.
    /// </summary>
    Null,

    /// <summary>
    ///     Plain or quoted text on one line, which the parser holds.
    /// </summary>
    Scalar,

    /// <summary>
    ///     A list, in brackets on one line or with one entry to a line.
    /// </summary>
    Sequence,
}
