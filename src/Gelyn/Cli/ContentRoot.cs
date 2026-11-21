namespace Gelyn.Cli;

/// <summary>
///     Resolves the content root, the directory every relative setting and path of a run is resolved against.
/// </summary>
internal static class ContentRoot
{
    private const string Variable = "DOTNET_CONTENTROOT";

    /// <summary>
    ///     Resolves the content root from <c>DOTNET_CONTENTROOT</c> when it is set, and from the current working
    ///     directory otherwise.
    /// </summary>
    /// <returns>
    ///     The absolute path of the content root, which need not exist.
    /// </returns>
    internal static string Resolve() =>
        Environment.GetEnvironmentVariable(Variable) is { Length: > 0 } root ? Path.GetFullPath(root) : Directory.GetCurrentDirectory();
}
