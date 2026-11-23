using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Gelyn.Core.Internals;

internal static partial class NativeMethods
{
    #region kernel32.dll

    /// <summary>
    ///     The standard output device, for <see cref="GetStdHandle"/>.
    /// </summary>
    internal const int STD_OUTPUT_HANDLE = -11;

    /// <summary>
    ///     The standard error device, for <see cref="GetStdHandle"/>.
    /// </summary>
    internal const int STD_ERROR_HANDLE = -12;

    /// <summary>
    ///     The handle <see cref="GetStdHandle"/> returns when it fails.
    /// </summary>
    internal const nint INVALID_HANDLE_VALUE = -1;

    /// <summary>
    ///     The console output mode under which the console interprets escape sequences instead of printing them.
    /// </summary>
    internal const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;

    /// <summary>
    ///     Retrieves the handle of a standard device.
    /// </summary>
    /// <param name="nStdHandle">
    ///     The device, <see cref="STD_OUTPUT_HANDLE"/> or <see cref="STD_ERROR_HANDLE"/>.
    /// </param>
    /// <returns>
    ///     The handle, <see cref="INVALID_HANDLE_VALUE"/> when the call fails, or <see cref="nint.Zero"/> when the
    ///     process has no such device.
    /// </returns>
    /// <remarks>
    ///     Decorated with <see cref="SuppressGCTransitionAttribute"/> because the call reads the process table and
    ///     never blocks, where the two mode calls are requests to the console host.
    /// </remarks>
    [SuppressGCTransition]
    [SupportedOSPlatform("windows")]
    [LibraryImport(Libraries.Kernel32)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial nint GetStdHandle(int nStdHandle);

    /// <summary>
    ///     Retrieves the mode of a console input or output buffer.
    /// </summary>
    /// <param name="hConsoleHandle">
    ///     The handle of the buffer.
    /// </param>
    /// <param name="lpMode">
    ///     Receives the mode flags.
    /// </param>
    /// <returns>
    ///     A nonzero value when the call succeeds, and zero when it fails, as for a handle that is not a console.
    /// </returns>
    [SupportedOSPlatform("windows")]
    [LibraryImport(Libraries.Kernel32)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial int GetConsoleMode(nint hConsoleHandle, out uint lpMode);

    /// <summary>
    ///     Sets the mode of a console input or output buffer.
    /// </summary>
    /// <param name="hConsoleHandle">
    ///     The handle of the buffer.
    /// </param>
    /// <param name="dwMode">
    ///     The mode flags.
    /// </param>
    /// <returns>
    ///     A nonzero value when the call succeeds, and zero when it fails, as for a flag the console does not
    ///     support.
    /// </returns>
    [SupportedOSPlatform("windows")]
    [LibraryImport(Libraries.Kernel32)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial int SetConsoleMode(nint hConsoleHandle, uint dwMode);

    #endregion // kernel32.dll
}
