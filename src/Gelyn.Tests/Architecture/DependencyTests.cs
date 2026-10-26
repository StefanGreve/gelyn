using ArchUnitNET.Fluent;
using ArchUnitNET.TUnit;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Gelyn.Tests.Architecture;

/// <summary>
///     Architecture tests for the direction in which the tool's namespaces depend on one another.
/// </summary>
public class DependencyTests : ArchBase
{
    #region Dependency Tests

    /// <summary>
    ///     Verifies that a namespace never depends on one that sits above it.
    /// </summary>
    /// <remarks>
    ///     The tool layers as commands over services over components over the model, each layer reaching
    ///     down and never up: a command drives a service, a service composes components, and a component
    ///     reads the model. The model carries data alone, so it depends on none of the three. Each pair
    ///     below names a layer and one of the layers it has to stay independent of.
    /// </remarks>
    [Test]
    [Arguments(ModelNamespace, CommandsNamespace)]
    [Arguments(ModelNamespace, ComponentsNamespace)]
    [Arguments(ModelNamespace, ServicesNamespace)]
    [Arguments(ComponentsNamespace, CommandsNamespace)]
    [Arguments(ComponentsNamespace, ServicesNamespace)]
    [Arguments(ServicesNamespace, CommandsNamespace)]
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
