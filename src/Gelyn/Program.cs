using System.CommandLine;
using System.Diagnostics;

using Gelyn.Cli;
using Gelyn.Commands;
using Gelyn.Core.Internals;
using Gelyn.Core.Internals.Terminal;
using Gelyn.Extensions;

namespace Gelyn;

/// <summary>
///     Entry point for the <c>gelyn</c> command line tool.
/// </summary>
internal static class Program
{
    /// <summary>
    ///     Runs the tool.
    /// </summary>
    /// <param name="args">
    ///     Command line arguments passed to the tool.
    /// </param>
    /// <returns>
    ///     Zero on success, a non-zero exit code otherwise.
    /// </returns>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = Justifications.ByDesign)]
    public static async Task<int> Main(string[] args)
    {
        long started = Stopwatch.GetTimestamp();

        ConsoleStream stdOut = ConsoleStream.StandardOutput;
        ConsoleStream stdErr = ConsoleStream.StandardError;

        try
        {
            // Lets a command's exception reach the catch block below, which writes one error line per problem and
            // picks the exit code. The default handler would print the stack trace and return 1.
            var configuration = new InvocationConfiguration { EnableDefaultExceptionHandler = false };

            ParseResult parsed = GelynCommand
                .Create(parseResult => Session.Open(parseResult, ContentRoot.Resolve(), stdOut, stdErr, started))
                .Parse(args);

            return await parsed
                .InvokeAsync(configuration)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            stdErr.WriteError(exception);
            return ExitCodes.For(exception);
        }
    }
}
