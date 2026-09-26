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
        // Declared for the help text and so that a misspelling is rejected. The value is consumed before the
        // host exists, in Program.AddConfigurationFile, because it selects a configuration source.
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
