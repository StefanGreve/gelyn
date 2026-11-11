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
    ///     The author the assembly names as its company, or <see langword="null"/> when it names none.
    /// </summary>
    internal static string? Author { get; } = typeof(Tool).Assembly
        .GetCustomAttribute<AssemblyCompanyAttribute>()
        ?.Company;

    /// <summary>
    ///     The repository the tool is developed in, or <see langword="null"/> when the build stamped none.
    /// </summary>
    internal static string? RepositoryUrl { get; } = ReadMetadata("RepositoryUrl");

    /// <summary>
    ///     The SPDX expression of the license the tool ships under, or <see langword="null"/> when the build
    ///     stamped none.
    /// </summary>
    internal static string? License { get; } = ReadMetadata("PackageLicenseExpression");

    /// <summary>
    ///     The commit the tool was built from, or <see langword="null"/> when the build ran outside a git
    ///     repository.
    /// </summary>
    internal static string? Revision { get; } = ReadMetadata("SourceRevisionId");

    /// <summary>
    ///     The version of the .NET SDK the tool was built with, or <see langword="null"/> when the build stamped none.
    /// </summary>
    internal static string? SdkVersion { get; } = ReadMetadata("NETCoreSdkVersion");

    /// <summary>
    ///     The culture every value is formatted and parsed against, so that what a build produces depends on
    ///     no setting of the machine it ran on.
    /// </summary>
    /// <remarks>
    ///     <c>InvariantGlobalization</c> already makes this the current culture of the published tool, but not
    ///     of a host that merely references the assembly, which is where stating it explicitly earns its place.
    /// </remarks>
    internal static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    /// <summary>
    ///     The line ending every generated document ends its lines on, independent of the host and the sources.
    /// </summary>
    /// <seealso cref="StringUtils.NormalizeLineEndings"/>
    internal const string LineEnding = "\n";

    #region Helpers

    private static string? ReadMetadata(string key) =>
        typeof(Tool).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == key)
            ?.Value;

    #endregion
}
