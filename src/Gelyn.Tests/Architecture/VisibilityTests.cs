using ArchUnitNET.Fluent;
using ArchUnitNET.TUnit;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Gelyn.Tests.Architecture;

/// <summary>
///     Architecture tests for the accessibility of the types the tool declares.
/// </summary>
public class VisibilityTests : ArchBase
{
    #region Visibility Tests

    /// <summary>
    ///     Verifies that every type in the namespace is internal.
    /// </summary>
    /// <remarks>
    ///     <c>ResideInNamespace</c> matches a namespace by its full name rather than as a prefix, so each one
    ///     has to be named in its own right to be covered at all, the options namespace below the model
    ///     included. <c>Gelyn.Core</c> is the one namespace left out, because the YamlDotNet static
    ///     generator requires <c>YamlContext</c> to be public.
    /// </remarks>
    /// <param name="namespaceName">
    ///     The full name of the namespace whose types are under test.
    /// </param>
    [Test]
    [Arguments(CommandsNamespace)]
    [Arguments(ComponentsNamespace)]
    [Arguments(ExtensionsNamespace)]
    [Arguments(InternalsNamespace)]
    [Arguments(ModelNamespace)]
    [Arguments(OptionsNamespace)]
    [Arguments(ServicesNamespace)]
    [Arguments(ValidatorsNamespace)]
    public void Visibility_Test_EveryTypeInTheNamespaceIsInternal(string namespaceName)
    {
        // Arrange
        IArchRule rule = Types()
            .That()
            .ResideInNamespace(namespaceName)
            .Should()
            .BeInternal();

        // Act and Assert
        rule.Check(Architecture);
    }

    #endregion // Visibility Tests
}
