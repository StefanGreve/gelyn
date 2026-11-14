using System;
using System.CommandLine;
using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Threading.Tasks;

using Gelyn.Commands;
using Gelyn.Core.Internals;
using Gelyn.Extensions;
using Gelyn.Model;
using Gelyn.Model.Options;
using Gelyn.Tests.Fixtures;
using Gelyn.Tests.Stubs;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Gelyn.Tests.Core.Internals;

/// <summary>
///     Unit tests for the <see cref="ConfigurationFile"/> class.
/// </summary>
public class ConfigurationFileTests
{
    #region Requested Tests

    /// <summary>
    ///     Verifies that the path given to <c>--config</c> is read wherever it stands, including behind a valueless
    ///     option, which the command line configuration provider pairs with the token after it.
    /// </summary>
    [Test]
    [Arguments("--info --config site.json")]
    [Arguments("--config site.json --info")]
    [Arguments("build --config site.json")]
    [Arguments("build --config:site.json")]
    [Arguments("build --config=site.json")]
    [Arguments("serve --launch-browser --config site.json")]
    public async Task Requested_Test_PathOnTheCommandLine_IsRead(string commandLine)
    {
        // Act
        string? requested = ConfigurationFile.Requested(commandLine.Split(' '));

        // Assert
        await Assert.That(requested).IsEqualTo("site.json");
    }

    /// <summary>
    ///     Verifies that a command line without <c>--config</c> requests no path, which leaves the default file.
    /// </summary>
    [Test]
    public async Task Requested_Test_WithoutTheOption_IsNull()
    {
        // Act
        string? requested = ConfigurationFile.Requested(["build"]);

        // Assert
        await Assert.That(requested).IsNull();
    }

    /// <summary>
    ///     Verifies that the parse that knows <c>--config</c> alone reads the path the full command reads, so that
    ///     the file the loader picks is the one the command line names.
    /// </summary>
    /// <remarks>
    ///     Parsed with the <see cref="GelynCommand"/> the tool composes, so that the comparison follows every option
    ///     the tool declares.
    /// </remarks>
    [Test]
    [Arguments("--info --config site.json")]
    [Arguments("build --title Home --config site.json")]
    [Arguments("build --feed --config site.json")]
    [Arguments("serve --launch-browser --port 9000 --config site.json")]
    [Arguments("clean --config site.json")]
    [Arguments("--help")]
    public async Task Requested_Test_PathOnTheCommandLine_MatchesTheFullCommand(string commandLine)
    {
        // Arrange
        string[] args = commandLine.Split(' ');

        // Act
        string? requested = ConfigurationFile.Requested(args);

        // Assert
        await Assert.That(requested).IsEqualTo(Compose().Parse(args).GetValue<string>(ConfigurationFile.OptionName));
    }

    /// <summary>
    ///     Verifies that a command line on which the two parses differ is one the full command rejects, so that no
    ///     command runs on the file the shorter parse picked.
    /// </summary>
    /// <remarks>
    ///     The full command takes <c>--config</c> as the missing value of <c>--title</c>, which the shorter parse
    ///     does not know and leaves alone.
    /// </remarks>
    [Test]
    public async Task Requested_Test_OptionMissingItsValue_IsRejectedByTheFullCommand()
    {
        // Arrange
        string[] args = ["build", "--title", ConfigurationFile.OptionName, "site.json"];

        // Act
        ParseResult full = Compose().Parse(args);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ConfigurationFile.Requested(args)).IsEqualTo("site.json");
            await Assert.That(full.Errors).IsNotEmpty();
        }
    }

    /// <summary>
    ///     Verifies that a path given in a response file is read, as it is by the full command.
    /// </summary>
    [Test]
    public async Task Requested_Test_PathInAResponseFile_IsRead()
    {
        // Arrange
        using DiskFixture fixture = DiskFixture.Create()
            .WriteOutside("options.rsp", "--config site.json");

        string[] args = ["build", $"@{Path.Combine(fixture.Root, "options.rsp")}"];

        // Act
        string? requested = ConfigurationFile.Requested(args);

        // Assert
        await Assert.That(requested).IsEqualTo("site.json");
    }

    /// <summary>
    ///     Verifies that the file named behind <c>--info</c> is the one the loader reads, which is the case the
    ///     command line configuration provider used to lose.
    /// </summary>
    [Test]
    public async Task Requested_Test_PathBehindInfo_ReachesTheLoader()
    {
        // Arrange
        using DiskFixture fixture = DiskFixture.Create()
            .WriteOutside("site.json", """{ "Title": "From site.json" }""");

        string path = Path.Combine(fixture.Root, "site.json");
        string[] args = ["--info", ConfigurationFile.OptionName, path];
        var configuration = new ConfigurationManager();
        var options = new SiteOptions();

        // Act
        ConfigurationFileStatus status = configuration.AddSiteConfiguration(args, ConfigurationFile.Requested(args));
        configuration.Bind(options);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(status.Exists).IsTrue();
            await Assert.That(status.Failure).IsNull();
            await Assert.That(options.Title).IsEqualTo("From site.json");
            await Assert.That(configuration[ConfigurationFile.PathKey]).IsEqualTo(path);
        }
    }

    #endregion // Requested Tests

    #region Helpers

    private static GelynCommand Compose()
    {
        var configuration = new ConfigurationManager();
        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IConfigurationRoot>(configuration);
        services.AddSingleton<IHostEnvironment>(new HostEnvironmentStub { ContentRootPath = Path.GetFullPath("/site") });
        services.AddSingleton<IFileSystem>(new MockFileSystem());
        services.AddKeyedSingleton(ConsoleTarget.StdOut, CapturedConsole.Create(TextWriter.Null));
        services.AddKeyedSingleton(ConsoleTarget.StdErr, CapturedConsole.Create(TextWriter.Null));
        services.AddSingleton(TimeProvider.System);
        services.AddLogging();
        services.AddConfigurationFile(new ConfigurationFileStatus { Path = Path.GetFullPath("/site/gelyn.json"), Optional = true, Exists = false, Failure = null });
        services.AddSiteOptions();
        services.AddServices();
        services.AddCommands();

        using ServiceProvider provider = services.BuildServiceProvider();

        return provider.GetRequiredService<GelynCommand>();
    }

    #endregion
}
