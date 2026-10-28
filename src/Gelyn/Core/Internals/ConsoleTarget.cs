namespace Gelyn.Core.Internals;

/// <summary>
///     Service keys for the two consoles the tool writes through.
/// </summary>
/// <remarks>
///     A consumer names the stream it means where the console is injected, so a message cannot reach the
///     wrong one by default. Both are registered in <see cref="Program"/>, which builds them before the
///     host so that a failure during startup still has a console to report itself on. The type is not
///     called <c>Console</c>, because <c>ImplicitUsings</c> puts <see cref="System.Console"/> in scope
///     everywhere and the two names would be ambiguous at every usage site.
/// </remarks>
internal static class ConsoleTarget
{
    /// <summary>
    ///     The console writing to standard output, which carries what the tool was asked to produce.
    /// </summary>
    internal const string StdOut = "stdout";

    /// <summary>
    ///     The console writing to standard error, which carries warnings and failures. Keeping these off
    ///     standard output is what lets a caller redirect a command's result without the diagnostics
    ///     landing in the same file.
    /// </summary>
    internal const string StdErr = "stderr";
}
