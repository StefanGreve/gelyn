using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Gelyn.Core.Internals;

namespace Gelyn.Tests.Core.Internals;

/// <summary>
///     Unit tests for the <see cref="Tool"/> class.
/// </summary>
public partial class ToolTests
{
    #region Metadata Tests

    /// <summary>
    ///     Verifies that the build stamps the author, the repository and the license into the assembly.
    /// </summary>
    [Test]
    public async Task Metadata_Test_AuthorRepositoryAndLicense_AreStamped()
    {
        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(Tool.Author).IsNotNullOrEmpty();
            await Assert.That(Tool.RepositoryUrl).IsNotNullOrEmpty();
            await Assert.That(Tool.License).IsNotNullOrEmpty();
        }
    }

    /// <summary>
    ///     Verifies that the stamped revision is a full commit hash.
    /// </summary>
    [Test]
    public async Task Revision_Test_IsAFullCommitHash()
    {
        // Arrange
        Skip.When(Tool.Revision is null, "Built outside a git repository, so no revision was stamped.");

        // Assert
        await Assert.That(Tool.Revision).Matches(CommitHash());
    }

    #endregion // Metadata Tests

    #region Helpers

    [GeneratedRegex("^[0-9a-f]{40}$")]
    private static partial Regex CommitHash();

    #endregion
}
