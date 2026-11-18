namespace Gelyn.Tests;

/// <summary>
///     Provides the category names the tests are filtered by.
/// </summary>
public static class Categories
{
    /// <summary>
    ///     Category for tests that enforce structural rules on the tool, such as the direction of namespace
    ///     dependencies, naming conventions and visibility.
    /// </summary>
    public const string Architecture = "architecture";

    /// <summary>
    ///     Category for tests that run against real infrastructure: the disk, a loopback listener, the process
    ///     environment or the working directory.
    /// </summary>
    public const string Integration = "integration";

    /// <summary>
    ///     Category for tests that verify the tool's own logic in isolation, against in-memory fixtures. The tool
    ///     has no public API, so this covers what a unit test covers elsewhere.
    /// </summary>
    public const string Internal = "internal";

    /// <summary>
    ///     Category for tests that enforce conventions on the test suite itself, such as every test carrying a
    ///     category.
    /// </summary>
    public const string Meta = "meta";

    /// <summary>
    ///     Category for tests that pin a bug that was fixed, so that it cannot return unnoticed.
    /// </summary>
    public const string Regression = "regression";
}
