using System.Globalization;
using System.Reflection;

namespace Gelyn.Core.Internals;

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

    /// <summary>
    ///     The culture every value is formatted and parsed against, so that what a build produces depends on
    ///     no setting of the machine it ran on.
    /// </summary>
    /// <remarks>
    ///     <c>InvariantGlobalization</c> already makes this the current culture of the published tool, but not
    ///     of a host that merely references the assembly, which is where stating it explicitly earns its place.
    /// </remarks>
    internal static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
}
