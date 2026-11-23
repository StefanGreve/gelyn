using System.Text;
using System.Threading.Channels;

using Gelyn.Core.Internals;

using Microsoft.Extensions.Logging;

namespace Gelyn.Diagnostics;

/// <summary>
///     Writes the records of a run to standard error, one line per record, from the level <c>--verbosity</c>
///     selects.
/// </summary>
[SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = $"{Justifications.ByDesign}. The writer is standard error, which the session owns.")]
internal sealed class DiagnosticLog : IDisposable
{
    private const string TimestampFormat = "HH:mm:ss.fff";

    private readonly TextWriter _writer;
    private readonly bool _colored;
    private readonly TimeProvider _timeProvider;
    private readonly Channel<string> _lines = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
    private object? _drainLock;
    private Task? _drain;
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DiagnosticLog"/> class.
    /// </summary>
    /// <param name="minimumLevel">
    ///     The lowest level that is written, as <c>--verbosity</c> selects it.
    /// </param>
    /// <param name="writer">
    ///     The writer the lines go to, which is standard error in a run.
    /// </param>
    /// <param name="colored">
    ///     Indicates whether the level of a line is colored with ANSI escape sequences, which a terminal renders
    ///     and a file would keep.
    /// </param>
    /// <param name="timeProvider">
    ///     The clock the timestamp of a line is read from.
    /// </param>
    internal DiagnosticLog(LogLevel minimumLevel, TextWriter writer, bool colored, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.MinimumLevel = minimumLevel;
        this._writer = writer;
        this._colored = colored;
        this._timeProvider = timeProvider;
    }

    /// <summary>
    ///     The lowest level that is written.
    /// </summary>
    public LogLevel MinimumLevel { get; }

    /// <summary>
    ///     Indicates whether a record of a level is written.
    /// </summary>
    /// <param name="level">
    ///     The level of the record.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> when the level is at or above <see cref="MinimumLevel"/> and is not
    ///     <see cref="LogLevel.None"/>, otherwise <see langword="false"/>.
    /// </returns>
    public bool IsEnabled(LogLevel level) => level != LogLevel.None && level >= this.MinimumLevel;

    /// <summary>
    ///     Creates the logger of one category.
    /// </summary>
    /// <typeparam name="T">
    ///     The type that logs through it, whose full name is the category.
    /// </typeparam>
    /// <returns>
    ///     A new logger that writes to this log.
    /// </returns>
    public ILogger<T> CreateLogger<T>() => new DiagnosticLogger<T>(this);

    /// <summary>
    ///     Queues one record as a line, whatever its level.
    /// </summary>
    /// <param name="level">
    ///     The level of the record, which the caller has checked with <see cref="IsEnabled"/>.
    /// </param>
    /// <param name="category">
    ///     The category of the record, the full name of the type that logs it.
    /// </param>
    /// <param name="eventId">
    ///     The id of the event the record reports.
    /// </param>
    /// <param name="message">
    ///     The formatted message.
    /// </param>
    /// <param name="exception">
    ///     The exception the record carries, written after the message on the same line, or
    ///     <see langword="null"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     <paramref name="level"/> is <see cref="LogLevel.None"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    ///     The log has been disposed.
    /// </exception>
    public void Write(LogLevel level, string category, int eventId, string message, Exception? exception)
    {
        ObjectDisposedException.ThrowIf(this._disposed, this);
        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(message);

        // Formatted here, on the thread that logs, and queued for the one task that writes the lines in order.
        string line = this.Format(level, category, eventId, message, exception);

        // The first record starts the drain. The guard keeps later calls from allocating the method group's delegate.
        if (this._drain is null) LazyInitializer.EnsureInitialized(ref this._drain, ref this._drainLock, this.StartDrain);

        this._lines.Writer.TryWrite(line);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this._disposed) return;

        this._disposed = true;
        this._lines.Writer.TryComplete();
        this._drain?.GetAwaiter().GetResult();
    }

    #region Helpers

    private static (string Name, string Colors) Describe(LogLevel level) => level switch
    {
        LogLevel.Trace => ("trce", AnsiCodes.BlackBackground + AnsiCodes.Gray),
        LogLevel.Debug => ("dbug", AnsiCodes.BlackBackground + AnsiCodes.Gray),
        LogLevel.Information => ("info", AnsiCodes.BlackBackground + AnsiCodes.DarkGreen),
        LogLevel.Warning => ("warn", AnsiCodes.BlackBackground + AnsiCodes.Bold + AnsiCodes.DarkYellow),
        LogLevel.Error => ("fail", AnsiCodes.DarkRedBackground + AnsiCodes.Black),
        LogLevel.Critical => ("crit", AnsiCodes.DarkRedBackground + AnsiCodes.Bold + AnsiCodes.Gray),
        _ => throw new ArgumentOutOfRangeException(nameof(level)),
    };

    private string Format(LogLevel level, string category, int eventId, string message, Exception? exception)
    {
        (string name, string colors) = Describe(level);
        var line = new StringBuilder(128);

        line.Append(this._timeProvider.GetLocalNow().ToString(TimestampFormat, Tool.Culture)).Append(' ');

        if (this._colored) line.Append(colors).Append(name).Append(AnsiCodes.Reset);
        else line.Append(name);

        line.Append(": ").Append(category).Append('[').Append(eventId).Append(']');

        if (message.Length > 0) line.Append(' ').Append(message.ReplaceLineEndings(" "));
        if (exception is not null) line.Append(' ').Append(exception.ToString().ReplaceLineEndings(" "));

        return line.Append(Environment.NewLine).ToString();
    }

    // An async method runs on the caller until its first pending await, so the hop to the pool is explicit and
    // the first producer never writes a queued line on its own thread.
    private Task StartDrain() => Task.Run(this.DrainAsync);

    private async Task DrainAsync()
    {
        try
        {
            await foreach (string line in this._lines.Reader.ReadAllAsync().ConfigureAwait(false))
                await this._writer.WriteAsync(line).ConfigureAwait(false);

            await this._writer.FlushAsync().ConfigureAwait(false);
        }
        catch (IOException)
        {
        }
    }

    #endregion
}
