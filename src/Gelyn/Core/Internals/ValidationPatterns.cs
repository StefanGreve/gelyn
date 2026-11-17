namespace Gelyn.Core.Internals;

/// <summary>
///     The patterns a setting is validated against, shared by the configuration schema and the build.
/// </summary>
internal static class ValidationPatterns
{
    /// <summary>
    ///     The shape of a BCP 47 language tag: a first subtag of letters, then any number of subtags of letters
    ///     and digits, each at most eight characters long and joined by a hyphen.
    /// </summary>
    internal const string LanguageTag = "^[A-Za-z]{1,8}(-[A-Za-z0-9]{1,8})*$";

    /// <summary>
    ///     The shape of a release version: three numbers of ASCII digits joined by a dot.
    /// </summary>
    internal const string Version = @"^[0-9]+\.[0-9]+\.[0-9]+$";

    /// <summary>
    ///     The shape of a GitHub repository: an owner and a name joined by a slash, neither holding a slash or
    ///     whitespace.
    /// </summary>
    internal const string Repository = @"^[^/\s]+/[^/\s]+$";
}
