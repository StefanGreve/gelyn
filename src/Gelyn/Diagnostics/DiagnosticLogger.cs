using Microsoft.Extensions.Logging;

namespace Gelyn.Diagnostics;

/// <summary>
///     The logger of one category, which writes its records through a <see cref="DiagnosticLog"/>.
/// </summary>
/// <typeparam name="T">
///     The type that logs through it, whose full name is the category.
/// </typeparam>
internal sealed class DiagnosticLogger<T> : ILogger<T>
{
    private static readonly string Category = typeof(T).FullName ?? typeof(T).Name;

    private readonly DiagnosticLog _log;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DiagnosticLogger{T}"/> class.
    /// </summary>
    /// <param name="log">
    ///     The log the records are written to.
    /// </param>
    internal DiagnosticLogger(DiagnosticLog log)
    {
        ArgumentNullException.ThrowIfNull(log);

        this._log = log;
    }

    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel) => this._log.IsEnabled(logLevel);

    /// <inheritdoc/>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        if (!this.IsEnabled(logLevel)) return;

        this._log.Write(logLevel, Category, eventId.Id, formatter(state, exception), exception);
    }
}
