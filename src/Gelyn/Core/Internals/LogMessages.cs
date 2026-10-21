using Microsoft.Extensions.Logging;

namespace Gelyn.Core.Internals;

/// <summary>
///     Every record the tool writes, as methods generated at compile time by
///     <see cref="LoggerMessageAttribute"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal static partial class LogMessages
{
    /// <summary>
    ///     Reports how long it took to locate the files the site is built from.
    /// </summary>
    /// <param name="logger">
    ///     The logger the record is written to.
    /// </param>
    /// <param name="itemCount">
    ///     The number of files the scan located.
    /// </param>
    /// <param name="elapsedMilliseconds">
    ///     The duration of the scan, which reads directory metadata only.
    /// </param>
    [LoggerMessage(
        EventId = EventIds.SCAN_COMPLETED,
        EventName = nameof(EventIds.SCAN_COMPLETED),
        Level = LogLevel.Debug,
        SkipEnabledCheck = true,
        Message = "Scanned {ItemCount} file(s) in {ElapsedMilliseconds:F2} ms")]
    internal static partial void LogScanCompleted(this ILogger logger, int itemCount, double elapsedMilliseconds);

    /// <summary>
    ///     Reports how long it took to read the located pages and render their Markdown.
    /// </summary>
    /// <param name="logger">
    ///     The logger the record is written to.
    /// </param>
    /// <param name="pageCount">
    ///     The number of pages that were rendered.
    /// </param>
    /// <param name="elapsedMilliseconds">
    ///     The duration of the render, including the time spent reading the files.
    /// </param>
    [LoggerMessage(
        EventId = EventIds.RENDER_COMPLETED,
        EventName = nameof(EventIds.RENDER_COMPLETED),
        Level = LogLevel.Debug,
        SkipEnabledCheck = true,
        Message = "Rendered {PageCount} page(s) in {ElapsedMilliseconds:F2} ms")]
    internal static partial void LogRenderCompleted(this ILogger logger, int pageCount, double elapsedMilliseconds);

    /// <summary>
    ///     Reports how long it took to compose the discovered pages into complete documents.
    /// </summary>
    /// <param name="logger">
    ///     The logger the record is written to.
    /// </param>
    /// <param name="pageCount">
    ///     The number of pages that were composed.
    /// </param>
    /// <param name="elapsedMilliseconds">
    ///     The accumulated duration of the composition, excluding the time spent writing.
    /// </param>
    [LoggerMessage(
        EventId = EventIds.COMPOSE_COMPLETED,
        EventName = nameof(EventIds.COMPOSE_COMPLETED),
        Level = LogLevel.Debug,
        SkipEnabledCheck = true,
        Message = "Composed {PageCount} page(s) in {ElapsedMilliseconds:F2} ms")]
    internal static partial void LogComposeCompleted(this ILogger logger, int pageCount, double elapsedMilliseconds);

    /// <summary>
    ///     Reports how long it took to write the generated documents and copy everything else located, both
    ///     of which land in the output directory.
    /// </summary>
    /// <param name="logger">
    ///     The logger the record is written to.
    /// </param>
    /// <param name="fileCount">
    ///     The number of files that were written and copied.
    /// </param>
    /// <param name="elapsedMilliseconds">
    ///     The accumulated duration of the writes and the copies, including the directories created to hold
    ///     them.
    /// </param>
    [LoggerMessage(
        EventId = EventIds.WRITE_COMPLETED,
        EventName = nameof(EventIds.WRITE_COMPLETED),
        Level = LogLevel.Debug,
        SkipEnabledCheck = true,
        Message = "Wrote {FileCount} file(s) in {ElapsedMilliseconds:F2} ms")]
    internal static partial void LogWriteCompleted(this ILogger logger, int fileCount, double elapsedMilliseconds);

    /// <summary>
    ///     Reports how long it took to delete the output that no located file accounts for.
    /// </summary>
    /// <param name="logger">
    ///     The logger the record is written to.
    /// </param>
    /// <param name="fileCount">
    ///     The number of files that were deleted, which excludes the directories emptied by deleting them.
    /// </param>
    /// <param name="elapsedMilliseconds">
    ///     The duration of the reconcile, which walks the whole output directory whether or not it deletes
    ///     anything.
    /// </param>
    [LoggerMessage(
        EventId = EventIds.REMOVE_COMPLETED,
        EventName = nameof(EventIds.REMOVE_COMPLETED),
        Level = LogLevel.Debug,
        SkipEnabledCheck = true,
        Message = "Removed {FileCount} file(s) in {ElapsedMilliseconds:F2} ms")]
    internal static partial void LogRemoveCompleted(this ILogger logger, int fileCount, double elapsedMilliseconds);

    /// <summary>
    ///     Reports how long the whole build took.
    /// </summary>
    /// <param name="logger">
    ///     The logger the record is written to.
    /// </param>
    /// <param name="fileCount">
    ///     The number of files the build wrote, copied and deleted, which is the same total the command
    ///     reports to standard output.
    /// </param>
    /// <param name="elapsedMilliseconds">
    ///     The duration of the build as a whole.
    /// </param>
    [LoggerMessage(
        EventId = EventIds.BUILD_COMPLETED,
        EventName = nameof(EventIds.BUILD_COMPLETED),
        Level = LogLevel.Debug,
        SkipEnabledCheck = true,
        Message = "Processed {FileCount} file(s) in {ElapsedMilliseconds:F2} ms")]
    internal static partial void LogBuildCompleted(this ILogger logger, int fileCount, double elapsedMilliseconds);

    /// <summary>
    ///     Reports how long the tool took to become ready to run a command.
    /// </summary>
    /// <param name="logger">
    ///     The logger the record is written to.
    /// </param>
    /// <param name="elapsedMilliseconds">
    ///     The duration from the first statement of <c>Program.Main</c> to the dispatch of the parsed command,
    ///     which excludes the process and runtime start that precedes it.
    /// </param>
    [LoggerMessage(
        EventId = EventIds.STARTUP_COMPLETED,
        EventName = nameof(EventIds.STARTUP_COMPLETED),
        Level = LogLevel.Debug,
        Message = "Started in {ElapsedMilliseconds:F2} ms")]
    internal static partial void LogStartupCompleted(this ILogger logger, double elapsedMilliseconds);

    /// <summary>
    ///     Reports the directory the preview server answers from and the address it answers on.
    /// </summary>
    /// <param name="logger">
    ///     The logger the record is written to.
    /// </param>
    /// <param name="directory">
    ///     The absolute path of the output directory, which is the only directory a request can reach.
    /// </param>
    /// <param name="address">
    ///     The bound address, which is a loopback prefix.
    /// </param>
    /// <remarks>
    ///     Declared without <c>SkipEnabledCheck</c>, as every record the preview server and its rebuild loop
    ///     write is: each argument is already in hand where it is called, so there is no evaluation a guard
    ///     could spare. Leaving the generated check in place keeps the level enforced here rather than at
    ///     every call site.
    /// </remarks>
    [LoggerMessage(
        EventId = EventIds.SERVER_STARTED,
        EventName = nameof(EventIds.SERVER_STARTED),
        Level = LogLevel.Debug,
        Message = "Serving {Directory} at {Address}")]
    internal static partial void LogServerStarted(this ILogger logger, string directory, string address);

    /// <summary>
    ///     Reports how many entries the watchers saw change before the debounce window closed.
    /// </summary>
    /// <param name="logger">
    ///     The logger the record is written to.
    /// </param>
    /// <param name="changeCount">
    ///     The number of distinct entries that changed, which is one for a single save and more for the burst
    ///     a formatter on save produces.
    /// </param>
    /// <param name="path">
    ///     The absolute path of the change that opened the window.
    /// </param>
    [LoggerMessage(
        EventId = EventIds.CHANGE_DETECTED,
        EventName = nameof(EventIds.CHANGE_DETECTED),
        Level = LogLevel.Debug,
        Message = "Detected {ChangeCount} change(s) starting at {Path}")]
    internal static partial void LogChangeDetected(this ILogger logger, int changeCount, string path);

    /// <summary>
    ///     Reports that the configuration file was read again, which is what makes the options monitor hand
    ///     out the values it now holds.
    /// </summary>
    /// <param name="logger">
    ///     The logger the record is written to.
    /// </param>
    /// <param name="path">
    ///     The absolute path of the configuration file that was read.
    /// </param>
    [LoggerMessage(
        EventId = EventIds.CONFIGURATION_RELOADED,
        EventName = nameof(EventIds.CONFIGURATION_RELOADED),
        Level = LogLevel.Debug,
        Message = "Reloaded {Path}")]
    internal static partial void LogConfigurationReloaded(this ILogger logger, string path);

    /// <summary>
    ///     Reports how long one rebuild took.
    /// </summary>
    /// <param name="logger">
    ///     The logger the record is written to.
    /// </param>
    /// <param name="fileCount">
    ///     The number of files the rebuild wrote, copied and deleted, which is the same total the one line it
    ///     reports to standard output carries.
    /// </param>
    /// <param name="elapsedMilliseconds">
    ///     The duration of the rebuild, covering the configuration reload that may precede it but not the
    ///     debounce window that does, which is deliberate waiting rather than work.
    /// </param>
    [LoggerMessage(
        EventId = EventIds.REBUILD_COMPLETED,
        EventName = nameof(EventIds.REBUILD_COMPLETED),
        Level = LogLevel.Debug,
        Message = "Rebuilt {FileCount} file(s) in {ElapsedMilliseconds:F2} ms")]
    internal static partial void LogRebuildCompleted(this ILogger logger, int fileCount, double elapsedMilliseconds);
}
