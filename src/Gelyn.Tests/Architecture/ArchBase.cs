using ArchUnitNET.Loader;

namespace Gelyn.Tests.Architecture;

/// <summary>
///     The base class every architecture test derives from.
/// </summary>
public abstract class ArchBase
{
    private protected const string InternalsNamespace = "Gelyn.Internals";
    private protected const string ModelNamespace = "Gelyn.Model";
    private protected const string OptionsNamespace = "Gelyn.Model.Options";
    private protected const string ServicesNamespace = "Gelyn.Services";

    /// <summary>
    ///     The assembly under test, read once for the whole test run.
    /// </summary>
    protected static ArchUnitNET.Domain.Architecture Architecture { get; } = new ArchLoader()
        .LoadAssemblies(typeof(Program).Assembly)
        .Build();
}
