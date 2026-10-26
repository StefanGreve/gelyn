using ArchUnitNET.Fluent;
using ArchUnitNET.TUnit;

using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Gelyn.Tests.Architecture;

/// <summary>
///     Architecture tests for the way the tool's types are declared.
/// </summary>
public class DeclarationTests : ArchBase
{
    #region Declaration Tests

    /// <summary>
    ///     Verifies that every class the tool declares is sealed.
    /// </summary>
    /// <remarks>
    ///     Nothing here is consumed polymorphically, and a sealed class lets the AOT compiler dispatch
    ///     calls directly, which is what <c>OptimizationPreference=Size</c> benefits from. A static class
    ///     satisfies the rule as declared, the compiler emitting it as abstract and sealed, so the rule
    ///     bites on the records and the service classes alone.
    /// </remarks>
    [Test]
    public void Declaration_Test_EveryClassIsSealed()
    {
        // Arrange
        IArchRule rule = Classes()
            .That()
            .ResideInNamespaceOrBelow(RootNamespace)
            .And()
            .AreNotNested()
            .Should()
            .BeSealed();

        // Act and Assert
        rule.Check(Architecture);
    }

    #endregion // Declaration Tests
}
