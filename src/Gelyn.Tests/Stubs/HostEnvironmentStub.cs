using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Gelyn.Tests.Stubs;

/// <summary>
///     A host environment whose every member is settable, so that a test can place the content root
///     wherever it needs it.
/// </summary>
internal sealed class HostEnvironmentStub : IHostEnvironment
{
    /// <inheritdoc/>
    public string ApplicationName { get; set; } = "Gelyn.Tests";

    /// <inheritdoc/>
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

    /// <inheritdoc/>
    public string ContentRootPath { get; set; } = string.Empty;

    /// <inheritdoc/>
    public string EnvironmentName { get; set; } = Environments.Development;
}
