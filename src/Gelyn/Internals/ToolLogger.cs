using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Logging;

namespace Gelyn.Internals;

/// <summary>
///     Every record the tool writes, as methods generated at compile time by
///     <see cref="LoggerMessageAttribute"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal static partial class ToolLogger
{
    /// <summary>
    ///     Reports how long it took to discover the content tree and render its Markdown.
    /// </summary>
    /// <param name="logger">
    ///     The logger the record is written to.
    /// </param>
    /// <param name="pageCount">
    ///     The number of pages the walk discovered.
    /// </param>
    /// <param name="elapsedMilliseconds">
    ///     The duration of the walk.
    /// </param>
    [LoggerMessage(
        EventId = EventIds.WALK_COMPLETED,
        EventName = nameof(EventIds.WALK_COMPLETED),
        Level = LogLevel.Debug,
        SkipEnabledCheck = true,
        Message = "Walked {PageCount} page(s) in {ElapsedMilliseconds:F2} ms")]
    internal static partial void LogWalkCompleted(this ILogger logger, int pageCount, double elapsedMilliseconds);

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
    ///     Reports how long it took to write the generated documents to the output directory.
    /// </summary>
    /// <param name="logger">
    ///     The logger the record is written to.
    /// </param>
    /// <param name="fileCount">
    ///     The number of files that were written.
    /// </param>
    /// <param name="elapsedMilliseconds">
    ///     The accumulated duration of the writes, including the directories created to hold them.
    /// </param>
    [LoggerMessage(
        EventId = EventIds.WRITE_COMPLETED,
        EventName = nameof(EventIds.WRITE_COMPLETED),
        Level = LogLevel.Debug,
        SkipEnabledCheck = true,
        Message = "Wrote {FileCount} file(s) in {ElapsedMilliseconds:F2} ms")]
    internal static partial void LogWriteCompleted(this ILogger logger, int fileCount, double elapsedMilliseconds);

    /// <summary>
    ///     Reports how long the whole build took.
    /// </summary>
    /// <param name="logger">
    ///     The logger the record is written to.
    /// </param>
    /// <param name="fileCount">
    ///     The number of files the build produced.
    /// </param>
    /// <param name="elapsedMilliseconds">
    ///     The duration of the build as a whole.
    /// </param>
    [LoggerMessage(
        EventId = EventIds.BUILD_COMPLETED,
        EventName = nameof(EventIds.BUILD_COMPLETED),
        Level = LogLevel.Debug,
        SkipEnabledCheck = true,
        Message = "Built {FileCount} file(s) in {ElapsedMilliseconds:F2} ms")]
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
        SkipEnabledCheck = true,
        Message = "Started in {ElapsedMilliseconds:F2} ms")]
    internal static partial void LogStartupCompleted(this ILogger logger, double elapsedMilliseconds);
}
