using ArchUnitNET.Fluent;
using ArchUnitNET.TUnit;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Gelyn.Tests.Architecture;

/// <summary>
///     Architecture tests for the names the tool gives its types.
/// </summary>
public class NamingTests : ArchBase
{
    #region Naming Tests

    /// <summary>
    ///     Verifies that a type carries the suffix its namespace implies.
    /// </summary>
    /// <remarks>
    ///     The suffix is what makes a namespace self-describing, and the rule catches a type filed under
    ///     the wrong one. Only the four namespaces named after what they hold are listed; the model, the
    ///     core and the options carry no such convention.
    /// </remarks>
    [Test]
    [Arguments(CommandsNamespace, "Command")]
    [Arguments(ComponentsNamespace, "Component")]
    [Arguments(ExtensionsNamespace, "Extensions")]
    [Arguments(ValidatorsNamespace, "Validator")]
    public void Naming_Test_EveryTypeCarriesTheSuffixOfItsNamespace(string namespaceName, string suffix)
    {
        // Arrange
        IArchRule rule = Types()
            .That()
            .ResideInNamespaceOrBelow(namespaceName)
            .And()
            .AreNotNested()
            .Should()
            .HaveNameEndingWith(suffix);

        // Act and Assert
        rule.Check(Architecture);
    }

    #endregion // Naming Tests
}
