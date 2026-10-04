using System;
using System.IO;
using System.Threading.Tasks;

using Gelyn.Model.Options;
using Gelyn.Tests.Stubs;
using Gelyn.Validators;

using Microsoft.Extensions.Options;

namespace Gelyn.Tests.Validators;

/// <summary>
///     Unit tests for the <see cref="SiteOptionsValidator"/> class.
/// </summary>
public class SiteOptionsValidatorTests
{
    #region Validate Tests

    /// <summary>
    ///     Verifies that an absolute web URL is accepted whether or not it carries a path.
    /// </summary>
    [Test]
    [Arguments("https://example.com")]
    [Arguments("http://example.com")]
    [Arguments("https://example.com/gelyn/docs")]
    public async Task Validate_Test_ServableBaseUrl_Succeeds(string baseUrl)
    {
        // Arrange
        SiteOptions options = new() { BaseUrl = new Uri(baseUrl) };

        // Act
        ValidateOptionsResult result = Validate(options);

        // Assert
        await Assert.That(result.Succeeded).IsTrue();
    }

    /// <summary>
    ///     Verifies that a URL carrying no origin to resolve a link against, or a scheme a reader's browser
    ///     cannot fetch, is rejected under a message naming the setting at fault.
    /// </summary>
    [Test]
    [Arguments("example.com")]
    [Arguments("/gelyn/docs")]
    [Arguments("file:///tmp/site")]
    [Arguments("ftp://example.com")]
    public async Task Validate_Test_UnservableBaseUrl_Fails(string baseUrl)
    {
        // Arrange
        SiteOptions options = new() { BaseUrl = new Uri(baseUrl, UriKind.RelativeOrAbsolute) };

        // Act
        ValidateOptionsResult result = Validate(options);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Failed).IsTrue();
            await Assert.That(result.FailureMessage).Contains(nameof(SiteOptions.BaseUrl));
        }
    }

    /// <summary>
    ///     Verifies that leaving the base URL unset is valid, because a site served from the root of a domain
    ///     needs none.
    /// </summary>
    [Test]
    public async Task Validate_Test_AbsentBaseUrl_Succeeds()
    {
        // Arrange
        SiteOptions options = new();

        // Act
        ValidateOptionsResult result = Validate(options);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(options.BaseUrl).IsNull();
            await Assert.That(result.Succeeded).IsTrue();
        }
    }

    /// <summary>
    ///     Verifies that an output directory resolving outside the content directory is accepted, including a
    ///     sibling whose name merely starts with it.
    /// </summary>
    [Test]
    [Arguments("content", "_site")]
    [Arguments("content", "content-old")]
    [Arguments("content", "../out")]
    public async Task Validate_Test_OutputDirectoryBesideContentDirectory_Succeeds(string content, string output)
    {
        // Arrange
        SiteOptions options = new() { ContentDirectory = content, OutputDirectory = output };

        // Act
        ValidateOptionsResult result = Validate(options);

        // Assert
        await Assert.That(result.Succeeded).IsTrue();
    }

    /// <summary>
    ///     Verifies that an output directory resolving to or inside the content directory is rejected under a
    ///     message naming the setting at fault, because the build would otherwise publish its own output as
    ///     content.
    /// </summary>
    [Test]
    [Arguments("content", "content/_site")]
    [Arguments("content", "content")]
    [Arguments("content/", "content")]
    [Arguments(".", "_site")]
    public async Task Validate_Test_OutputDirectoryInsideContentDirectory_Fails(string content, string output)
    {
        // Arrange
        SiteOptions options = new() { ContentDirectory = content, OutputDirectory = output };

        // Act
        ValidateOptionsResult result = Validate(options);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(result.Failed).IsTrue();
            await Assert.That(result.FailureMessage).Contains(nameof(SiteOptions.OutputDirectory));
        }
    }

    #endregion // Validate Tests

    #region Helpers

    // An explicit content root is required: the stub defaults it to string.Empty, which would leave
    // GetFullPath resolving the relative settings against the working directory.
    private static ValidateOptionsResult Validate(SiteOptions options) =>
        new SiteOptionsValidator(new HostEnvironmentStub { ContentRootPath = Path.GetFullPath("/site") })
            .Validate(name: null, options);

    #endregion
}
