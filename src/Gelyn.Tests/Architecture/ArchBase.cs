using ArchUnitNET.Loader;

namespace Gelyn.Tests.Architecture;

/// <summary>
///     The base class every architecture test derives from.
/// </summary>
public abstract class ArchBase
{
    /// <summary>
    ///     The assembly under test, read once for the whole test run.
    /// </summary>
    protected static ArchUnitNET.Domain.Architecture Architecture { get; } = new ArchLoader()
        .LoadAssemblies(typeof(Program).Assembly)
        .Build();
}
