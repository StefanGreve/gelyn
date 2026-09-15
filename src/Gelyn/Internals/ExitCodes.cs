namespace Gelyn.Internals;

/// <summary>
///     Process exit codes returned by the tool's commands.
/// </summary>
internal static class ExitCodes
{
    /// <summary>
    ///     The command completed successfully.
    /// </summary>
    internal const int Success = 0;

    /// <summary>
    ///     The command failed. Matches what <c>System.CommandLine</c> itself returns for parse errors, so a
    ///     caller cannot tell the two apart.
    /// </summary>
    internal const int Error = 1;
}
