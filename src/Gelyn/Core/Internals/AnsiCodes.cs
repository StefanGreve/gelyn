namespace Gelyn.Core.Internals;

/// <summary>
///     The escape sequences the tool writes to color a terminal.
/// </summary>
internal static class AnsiCodes
{
    /// <summary>
    ///     Switches on bold, which a terminal with sixteen colors renders as the bright variant.
    /// </summary>
    internal const string Bold = "\e[1m";

    /// <summary>
    ///     Sets the foreground to black.
    /// </summary>
    internal const string Black = "\e[30m";

    /// <summary>
    ///     Sets the foreground to dark red.
    /// </summary>
    internal const string DarkRed = "\e[31m";

    /// <summary>
    ///     Sets the foreground to dark green.
    /// </summary>
    internal const string DarkGreen = "\e[32m";

    /// <summary>
    ///     Sets the foreground to yellow.
    /// </summary>
    internal const string Yellow = "\e[33m";

    /// <summary>
    ///     Sets the foreground to gray.
    /// </summary>
    internal const string Gray = "\e[37m";

    /// <summary>
    ///     Sets the background to black.
    /// </summary>
    internal const string BlackBackground = "\e[40m";

    /// <summary>
    ///     Sets the background to dark red.
    /// </summary>
    internal const string DarkRedBackground = "\e[41m";

    /// <summary>
    ///     Restores the default foreground, weight and background.
    /// </summary>
    internal const string Reset = "\e[39m\e[22m\e[49m";
}
