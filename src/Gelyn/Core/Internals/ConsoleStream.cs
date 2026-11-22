using System.Runtime.Versioning;

namespace Gelyn.Core.Internals;

/// <summary>
///     Represents one standard stream of the process, with whether it is a terminal and whether it colors.
/// </summary>
/// <remarks>
///     Unless plain text is requested, a stream that is a terminal and renders escape sequences gets colors.
/// </remarks>
internal sealed class ConsoleStream
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ConsoleStream"/> class.
    /// </summary>
    /// <param name="writer">
    ///     The writer the stream writes through.
    /// </param>
    /// <param name="isTerminal">
    ///     Indicates whether the stream is a terminal and not a file or a pipe.
    /// </param>
    /// <param name="useColors">
    ///     Indicates whether the stream colors its text with escape sequences.
    /// </param>
    public ConsoleStream(TextWriter writer, bool isTerminal, bool useColors)
    {
        ArgumentNullException.ThrowIfNull(writer);

        this.Writer = writer;
        this.IsTerminal = isTerminal;
        this.UseColors = useColors;
    }

    /// <summary>
    ///     The writer the stream writes through.
    /// </summary>
    public TextWriter Writer { get; }

    /// <summary>
    ///     Indicates whether the stream is a terminal and not a file or a pipe.
    /// </summary>
    public bool IsTerminal { get; }

    /// <summary>
    ///     Indicates whether the stream colors its text with escape sequences.
    /// </summary>
    public bool UseColors { get; }

    /// <summary>
    ///     The standard output stream of the process, with its terminal and its colors detected on the first
    ///     read.
    /// </summary>
    public static ConsoleStream StandardOutput =>
        field ??= Open(Console.Out, Console.IsOutputRedirected, error: false);

    /// <summary>
    ///     The standard error stream of the process, with its terminal and its colors detected on the first
    ///     read.
    /// </summary>
    public static ConsoleStream StandardError =>
        field ??= Open(Console.Error, Console.IsErrorRedirected, error: true);

    /// <summary>
    ///     Decides whether a stream colors, from what the detection read.
    /// </summary>
    /// <param name="isTerminal">
    ///     Indicates whether the stream is a terminal.
    /// </param>
    /// <param name="rendersSequences">
    ///     Indicates whether the terminal renders escape sequences.
    /// </param>
    /// <param name="noColor">
    ///     The value of <c>NO_COLOR</c>. Any value but the empty string switches colors off, <c>0</c> included.
    /// </param>
    /// <param name="term">
    ///     The value of <c>TERM</c>, which switches colors off when it names a dumb terminal.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> when the stream colors, <see langword="false"/> otherwise.
    /// </returns>
    /// <seealso href="https://no-color.org/">NO_COLOR</seealso>
    internal static bool SupportsColors(bool isTerminal, bool rendersSequences, string? noColor, string? term) =>
        isTerminal && rendersSequences && string.IsNullOrEmpty(noColor) && !string.Equals(term, "dumb", StringComparison.Ordinal);

    /// <summary>
    ///     Wraps a text in escape sequences when the stream colors, and returns it as given otherwise.
    /// </summary>
    /// <param name="text">
    ///     The text to color.
    /// </param>
    /// <param name="codes">
    ///     The sequences that set the color or the weight, as <see cref="AnsiCodes"/> lists them.
    /// </param>
    /// <returns>
    ///     The text, colored when the stream colors.
    /// </returns>
    public string Paint(string text, string codes) =>
        this.UseColors ? codes + text + AnsiCodes.Reset : text;

    /// <summary>
    ///     Writes one line.
    /// </summary>
    /// <param name="line">
    ///     The line, without its line break.
    /// </param>
    public void WriteLine(string line) =>
        this.Writer.WriteLine(line);

    #region Helpers

    private static ConsoleStream Open(TextWriter writer, bool redirected, bool error)
    {
        bool isTerminal = !redirected;
        bool rendersSequences = isTerminal && (!OperatingSystem.IsWindows() || TryEnableVirtualTerminal(error));
        bool useColors = SupportsColors(isTerminal, rendersSequences, Environment.GetEnvironmentVariable("NO_COLOR"), Environment.GetEnvironmentVariable("TERM"));

        return new ConsoleStream(writer, isTerminal, useColors);
    }

    // A classic console refuses the mode and would print a sequence as text, so the stream stays plain there.
    [SupportedOSPlatform("windows")]
    private static bool TryEnableVirtualTerminal(bool error)
    {
        nint console = NativeMethods.GetStdHandle(error ? NativeMethods.STD_ERROR_HANDLE : NativeMethods.STD_OUTPUT_HANDLE);

        if (console == nint.Zero || console == NativeMethods.INVALID_HANDLE_VALUE) return false;
        if (NativeMethods.GetConsoleMode(console, out uint mode) == 0) return false;
        if ((mode & NativeMethods.ENABLE_VIRTUAL_TERMINAL_PROCESSING) != 0) return true;

        return NativeMethods.SetConsoleMode(console, mode | NativeMethods.ENABLE_VIRTUAL_TERMINAL_PROCESSING) != 0;
    }

    #endregion
}
