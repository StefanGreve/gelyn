using System.IO;
using System.Text;

using Spectre.Console;

namespace Gelyn.Tests.Stubs;

/// <summary>
///     A console output that reports itself as a terminal or as redirected, as a test sets it.
/// </summary>
internal sealed class AnsiConsoleOutputStub : IAnsiConsoleOutput
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="AnsiConsoleOutputStub"/> class.
    /// </summary>
    /// <param name="writer">
    ///     Receives everything the console writes.
    /// </param>
    /// <param name="isTerminal">
    ///     Indicates whether the output reports itself as a terminal.
    /// </param>
    public AnsiConsoleOutputStub(TextWriter writer, bool isTerminal)
    {
        this.Writer = writer;
        this.IsTerminal = isTerminal;
    }

    /// <inheritdoc/>
    public TextWriter Writer { get; }

    /// <inheritdoc/>
    public bool IsTerminal { get; }

    /// <inheritdoc/>
    public int Width => 80;

    /// <inheritdoc/>
    public int Height => 24;

    /// <inheritdoc/>
    public void SetEncoding(Encoding encoding)
    {
    }
}
