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
    ///     Verifies that every type the tool declares is internal.
    /// </summary>
    /// <remarks>
    ///     <c>ResideInNamespaceOrBelow</c> matches the root namespace as a prefix, so a namespace added
    ///     later is covered without being named. Nested types are exempt because the SharpYaml source
    ///     generator emits its state as private nested types inside <c>YamlContext</c>; an internal
    ///     declaring type already bounds how accessible they can be.
    /// </remarks>
    [Test]
    public void Visibility_Test_EveryTypeIsInternal()
    {
        // Arrange
        IArchRule rule = Types()
            .That()
            .ResideInNamespaceOrBelow(RootNamespace)
            .And()
            .AreNotNested()
            .Should()
            .BeInternal();

        // Act and Assert
        rule.Check(Architecture);
    }

    #endregion // Visibility Tests
}
