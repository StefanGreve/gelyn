using System.Reflection;

namespace Gelyn.Internals;

/// <summary>
///     The identity of the running tool.
/// </summary>
internal static class Tool
{
    /// <summary>
    ///     The name of the tool, in its canonical spelling.
    /// </summary>
    internal const string Name = "Gelyn";

    /// <summary>
    ///     The informational version of the assembly, or <c>0.0.0</c> when the assembly declares none.
    /// </summary>
    /// <remarks>
    ///     Read once on first access, so the value is fixed for the lifetime of the process.
    /// </remarks>
    internal static string Version { get; } = typeof(Tool).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion ?? "0.0.0";
}
