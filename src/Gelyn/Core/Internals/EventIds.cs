using System.Diagnostics.CodeAnalysis;

namespace Gelyn.Core.Internals;

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
    ///     The content tree has been scanned and every file it holds located.
    /// </summary>
    internal const int SCAN_COMPLETED = 1_000;

    /// <summary>
    ///     Every located page has been rendered from Markdown.
    /// </summary>
    internal const int RENDER_COMPLETED = 1_001;

    /// <summary>
    ///     Every page has been composed into a complete HTML document.
    /// </summary>
    internal const int COMPOSE_COMPLETED = 1_002;

    /// <summary>
    ///     Every document has been written and every other located file copied to the output directory.
    /// </summary>
    internal const int WRITE_COMPLETED = 1_003;

    /// <summary>
    ///     Every file in the output directory that no located file accounts for has been deleted.
    /// </summary>
    internal const int REMOVE_COMPLETED = 1_004;

    /// <summary>
    ///     The build has finished.
    /// </summary>
    internal const int BUILD_COMPLETED = 1_005;

    /// <summary>
    ///     The host has been built and a command is about to be dispatched.
    /// </summary>
    internal const int STARTUP_COMPLETED = 1_006;

    #endregion

    #region Serving (1_100 - 1_199)

    /// <summary>
    ///     The preview server has bound its address and is ready to answer requests.
    /// </summary>
    internal const int SERVER_STARTED = 1_100;

    #endregion
}
