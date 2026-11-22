using ArchUnitNET.Fluent;
using ArchUnitNET.TUnit;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Gelyn.Tests.Architecture;

/// <summary>
///     Architecture tests for the direction in which the tool's namespaces depend on one another.
/// </summary>
[Category(Categories.Architecture)]
public class DependencyTests : ArchBase
{
    #region Dependency Tests

    /// <summary>
    ///     Verifies that a namespace never depends on one that sits above it.
    /// </summary>
    /// <remarks>
    ///     The tool layers as commands over services over components over the model, each layer reaching
    ///     down and never up: a command drives a service, a service composes components, and a component
    ///     reads the model. The model carries data alone, so it depends on none of the three, and the
    ///     abstractions and the exceptions sit beside it, the former each implemented by one service. The
    ///     diagnostics sit beside the services,
    ///     which time and report through them, out of reach of the rendering layers. The command line layer
    ///     parses the arguments and composes the services for the commands that open it, so it reaches down
    ///     to every other layer and stays independent of the commands. Each pair below names a layer and one
    ///     of the layers it has to stay independent of.
    /// </remarks>
    [Test]
    [Arguments(AbstractionsNamespace, CliNamespace)]
    [Arguments(AbstractionsNamespace, CommandsNamespace)]
    [Arguments(AbstractionsNamespace, ComponentsNamespace)]
    [Arguments(AbstractionsNamespace, ServicesNamespace)]
    [Arguments(ModelNamespace, CliNamespace)]
    [Arguments(ModelNamespace, CommandsNamespace)]
    [Arguments(ModelNamespace, ComponentsNamespace)]
    [Arguments(ModelNamespace, DiagnosticsNamespace)]
    [Arguments(ModelNamespace, ServicesNamespace)]
    [Arguments(ExceptionsNamespace, CliNamespace)]
    [Arguments(ExceptionsNamespace, CommandsNamespace)]
    [Arguments(ExceptionsNamespace, ComponentsNamespace)]
    [Arguments(ExceptionsNamespace, DiagnosticsNamespace)]
    [Arguments(ExceptionsNamespace, ServicesNamespace)]
    [Arguments(ComponentsNamespace, CliNamespace)]
    [Arguments(ComponentsNamespace, CommandsNamespace)]
    [Arguments(ComponentsNamespace, DiagnosticsNamespace)]
    [Arguments(ComponentsNamespace, ServicesNamespace)]
    [Arguments(ServicesNamespace, CliNamespace)]
    [Arguments(ServicesNamespace, CommandsNamespace)]
    [Arguments(CoreNamespace, CliNamespace)]
    [Arguments(CoreNamespace, CommandsNamespace)]
    [Arguments(CoreNamespace, DiagnosticsNamespace)]
    [Arguments(CoreNamespace, ServicesNamespace)]
    [Arguments(DiagnosticsNamespace, CliNamespace)]
    [Arguments(DiagnosticsNamespace, CommandsNamespace)]
    [Arguments(DiagnosticsNamespace, ServicesNamespace)]
    [Arguments(CliNamespace, CommandsNamespace)]
    public void Dependency_Test_TheLowerNamespaceDoesNotDependOnTheHigherOne(string lower, string higher)
    {
        // Arrange
        IArchRule rule = Types()
            .That()
            .ResideInNamespaceOrBelow(lower)
            .Should()
            .NotDependOnAnyTypesThat()
            .ResideInNamespaceOrBelow(higher);

        // Act and Assert
        rule.Check(Architecture);
    }

    #endregion // Dependency Tests
}
