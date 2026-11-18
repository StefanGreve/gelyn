using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Gelyn.Tests.Meta;

/// <summary>
///     Tests for the categories the test suite is filtered by.
/// </summary>
[Category(Categories.Meta)]
public class CategoryTests
{
    #region Category Tests

    /// <summary>
    ///     Verifies that every test carries at least one category, on itself or on its class, so that no filtered
    ///     run can leave a test out of every category.
    /// </summary>
    [Test]
    public async Task Categories_Test_EveryTest_CarriesOne()
    {
        // Arrange
        MethodInfo[] tests = CollectTests();

        // Act
        string[] uncategorized = [.. tests
            .Where(static test => !ReadCategories(test).Any())
            .Select(static test => $"{test.DeclaringType?.Name}.{test.Name}")];

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(tests).IsNotEmpty();
            await Assert.That(uncategorized).IsEmpty();
        }
    }

    /// <summary>
    ///     Verifies that every category a test carries is one that <see cref="Categories"/> declares, so that a
    ///     misspelled category cannot hide a test from the run that filters for the right spelling.
    /// </summary>
    [Test]
    public async Task Categories_Test_EveryCategory_IsDeclared()
    {
        // Arrange
        HashSet<string?> declared = [.. typeof(Categories)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(static field => field.IsLiteral)
            .Select(static field => (string?)field.GetRawConstantValue())];

        // Act
        string[] undeclared = [.. CollectTests()
            .SelectMany(ReadCategories)
            .Where(category => !declared.Contains(category))
            .Distinct()];

        // Assert
        await Assert.That(undeclared).IsEmpty();
    }

    #endregion // Category Tests

    #region Helpers

    private static MethodInfo[] CollectTests() =>
    [
        .. typeof(CategoryTests).Assembly
            .GetTypes()
            .SelectMany(static type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(static method => method.IsDefined(typeof(TestAttribute), inherit: false)),
    ];

    private static IEnumerable<string> ReadCategories(MethodInfo test) =>
        test.GetCustomAttributes<CategoryAttribute>()
            .Concat(test.DeclaringType?.GetCustomAttributes<CategoryAttribute>() ?? [])
            .Select(static attribute => attribute.Category);

    #endregion
}
