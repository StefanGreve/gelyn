namespace Gelyn.Core.Interop;

/// <summary>
///     The native functions the tool calls, one file per library.
/// </summary>
internal static partial class NativeMethods
{
    /// <summary>
    ///     The names of the native libraries.
    /// </summary>
    private static class Libraries
    {
        /// <summary>
        ///     The Windows kernel library.
        /// </summary>
        internal const string Kernel32 = "kernel32.dll";
    }
}
