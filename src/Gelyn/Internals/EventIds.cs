using System.Diagnostics.CodeAnalysis;

namespace Gelyn.Internals;

/// <summary>
///     Identifiers for the log records the generator emits.
/// </summary>
/// <remarks>
///     Every identifier stays inside the range that a Windows event log and the <c>EventSource</c> provider
///     accept, so that either sink remains usable later without renumbering.
/// </remarks>
[ExcludeFromCodeCoverage]
internal static class EventIds
{
    #region Monitoring (1_000 - 1_099)

    /// <summary>
    ///     The content tree has been walked and every page rendered from Markdown.
    /// </summary>
    internal const int WALK_COMPLETED = 1_000;

    /// <summary>
    ///     Every page has been composed into a complete HTML document.
    /// </summary>
    internal const int COMPOSE_COMPLETED = 1_001;

    /// <summary>
    ///     Every document has been written to the output directory.
    /// </summary>
    internal const int WRITE_COMPLETED = 1_002;

    /// <summary>
    ///     The build has finished.
    /// </summary>
    internal const int BUILD_COMPLETED = 1_003;

    #endregion
}
