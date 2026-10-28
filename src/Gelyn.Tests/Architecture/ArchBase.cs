using ArchUnitNET.Loader;

namespace Gelyn.Tests.Architecture;

/// <summary>
///     The base class every architecture test derives from.
/// </summary>
public abstract class ArchBase
{
    private protected const string RootNamespace = "Gelyn";
    private protected const string CommandsNamespace = "Gelyn.Commands";
    private protected const string ComponentsNamespace = "Gelyn.Components";
    private protected const string ContextNamespace = "Gelyn.Core.Context";
    private protected const string ExtensionsNamespace = "Gelyn.Extensions";
    private protected const string InternalsNamespace = "Gelyn.Core.Internals";
    private protected const string ModelNamespace = "Gelyn.Model";
    private protected const string OptionsNamespace = "Gelyn.Model.Options";
    private protected const string ServicesNamespace = "Gelyn.Services";
    private protected const string ValidatorsNamespace = "Gelyn.Validators";

    /// <summary>
    ///     The assembly under test, read once for the whole test run.
    /// </summary>
    protected static ArchUnitNET.Domain.Architecture Architecture { get; } = new ArchLoader()
        .LoadAssemblies(typeof(Program).Assembly)
        .Build();
}
