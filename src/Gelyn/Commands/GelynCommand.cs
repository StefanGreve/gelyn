using System.CommandLine;
using System.Diagnostics.CodeAnalysis;

using Gelyn.Internals;

namespace Gelyn.Commands;

/// <summary>
///     The root command of the <c>gelyn</c> tool.
/// </summary>
[SuppressMessage("Performance", "CA1812", Justification = Justifications.ByDesign)]
internal sealed class GelynCommand : RootCommand
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="GelynCommand"/> class.
    /// </summary>
    /// <param name="buildCommand">
    ///     The <c>build</c> subcommand.
    /// </param>
    public GelynCommand(BuildCommand buildCommand) : base("A static site generator.")
    {
        // Read by AddSiteConfiguration before the host exists, because it selects a configuration source.
        Option<string> configuration = new(ConfigurationFile.OptionName)
        {
            Description = $"Path to the configuration file. Defaults to {ConfigurationFile.DefaultFileName}.",
            HelpName = "path",
            Recursive = true,
        };

        this.Options.Add(configuration);
        this.Subcommands.Add(buildCommand);
    }
}
