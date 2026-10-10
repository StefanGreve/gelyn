namespace Gelyn.Core.Internals;

/// <summary>
///     Shared justifications for <see cref="System.Diagnostics.CodeAnalysis.SuppressMessageAttribute"/> annotations.
/// </summary>
internal static class Justifications
{
    /// <summary>
    ///     The flagged construct is intentional; the analyzer's recommendation does not apply here.
    /// </summary>
    internal const string ByDesign = "By Design";

    /// <summary>
    ///     A library this project builds on imposes the flagged construct.
    /// </summary>
    internal const string LibraryImposed = "Library Imposed";
}
