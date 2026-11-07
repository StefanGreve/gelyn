using Microsoft.Extensions.Options;

namespace Gelyn.Core.Internals;

/// <summary>
///     Process exit codes returned by the tool's commands.
/// </summary>
/// <remarks>
///     Numbered sequentially, so that a new code takes the next free value. Values from 126 upwards are
///     reserved by the shell for its own failures and are therefore left alone.
/// </remarks>
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

    /// <summary>
    ///     The configuration is invalid. Distinct from <see cref="Error"/>, so that a caller can tell a
    ///     rejected configuration from a parse failure.
    /// </summary>
    internal const int ConfigurationError = 2;

    /// <summary>
    ///     The exit code that reports a failure no command handled itself.
    /// </summary>
    /// <remarks>
    ///     The binder wraps a value it cannot convert in an <see cref="InvalidOperationException"/>, so the
    ///     <see cref="FormatException"/> inside is what tells it from one a bug raised.
    /// </remarks>
    /// <param name="exception">
    ///     The exception the command failed with.
    /// </param>
    /// <returns>
    ///     <see cref="ConfigurationError"/> when the configuration was rejected, <see cref="Error"/> otherwise.
    /// </returns>
    internal static int For(Exception exception) =>
        exception switch
        {
            OptionsValidationException or FileNotFoundException or InvalidDataException => ConfigurationError,
            InvalidOperationException { InnerException: FormatException } => ConfigurationError,
            _ => Error,
        };
}
