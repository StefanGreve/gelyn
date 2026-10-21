using System.ComponentModel;
using System.Diagnostics;

namespace Gelyn.Core.Internals;

/// <summary>
///     Opens a URL in whatever browser the machine has registered for it.
/// </summary>
internal static class Browser
{
    /// <summary>
    ///     Launches one URL in the default browser.
    /// </summary>
    /// <param name="url">
    ///     The absolute URL to open.
    /// </param>
    /// <param name="errorMessage">
    ///     Receives the message the refusal carried, or <see langword="null"/> when the URL was launched.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> when the shell took the URL, <see langword="false"/> otherwise.
    /// </returns>
    internal static bool TryLaunch(string url, [NotNullWhen(false)] out string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(url);

        try
        {
            // Null when the shell handed the URL to a browser that was already running
            using Process? process = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            errorMessage = null;
            return true;
        }
        catch (Exception exception) when (exception is Win32Exception or PlatformNotSupportedException)
        {
            errorMessage = exception.Message;
            return false;
        }
    }
}
