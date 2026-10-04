using System.Reflection;

namespace Gelyn.Internals;

/// <summary>
///     The version of the running tool.
/// </summary>
internal static class ToolVersion
{
    /// <summary>
    ///     The informational version of the assembly, which is also what <c>--version</c> reports.
    /// </summary>
    internal static string Current { get; } = typeof(ToolVersion).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion ?? "0.0.0";
}
