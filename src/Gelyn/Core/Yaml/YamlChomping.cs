namespace Gelyn.Core.Yaml;

/// <summary>
///     The ways a block scalar treats the line breaks at its end.
/// </summary>
/// <seealso href="https://yaml.org/spec/1.2.2/#8112-block-chomping-indicator"/>
internal enum YamlChomping
{
    /// <summary>
    ///     Keeps the line break of the last line of text and drops the blank lines after it. The default.
    /// </summary>
    Clip,

    /// <summary>
    ///     Drops every final line break, which the indicator <c>-</c> asks for.
    /// </summary>
    Strip,

    /// <summary>
    ///     Keeps every final line break, which the indicator <c>+</c> asks for.
    /// </summary>
    Keep,
}
