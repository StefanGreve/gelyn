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
    ///     Verifies that every class the tool declares is sealed, the abstract contracts aside.
    /// </summary>
    /// <remarks>
    ///     A sealed class lets the AOT compiler dispatch calls directly, which is what
    ///     <c>OptimizationPreference=Size</c> benefits from. A static class satisfies the rule as declared,
    ///     the compiler emitting it as abstract and sealed, so the rule bites on the records and the service
    ///     classes alone. An abstract class cannot be sealed, and each contract has one sealed implementation,
    ///     which <see cref="Declaration_Test_AbstractClassesAndAbstractionsCoincide"/> keeps in its place.
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
            .And()
            .AreNotAbstract()
            .Should()
            .BeSealed();

        // Act and Assert
        rule.Check(Architecture);
    }

    /// <summary>
    ///     Verifies that the abstract classes are the classes under <c>Gelyn.Abstractions</c>, and no others.
    /// </summary>
    /// <remarks>
    ///     A static class is emitted as abstract and sealed, so the first rule leaves the sealed ones out.
    /// </remarks>
    [Test]
    public void Declaration_Test_AbstractClassesAndAbstractionsCoincide()
    {
        // Arrange
        IArchRule abstractions = Classes()
            .That()
            .AreAbstract()
            .And()
            .AreNotSealed()
            .And()
            .AreNotNested()
            .Should()
            .ResideInNamespace(AbstractionsNamespace);

        IArchRule bases = Classes()
            .That()
            .ResideInNamespace(AbstractionsNamespace)
            .Should()
            .BeAbstract();

        // Act and Assert
        abstractions.And(bases).Check(Architecture);
    }

    #endregion // Declaration Tests
}
