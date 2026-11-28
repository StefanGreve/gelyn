using System.CommandLine;

using Gelyn.Cli;

namespace Gelyn.Commands;

/// <summary>
///     The root command of the <c>gelyn</c> tool.
/// </summary>
internal sealed class GelynCommand : RootCommand
{
    private GelynCommand(Func<ParseResult, Session> open)
        : base("A static site generator.")
    {
        this.Options.Add(ConfigurationFile.CreateOption());
        this.Options.Add(VerbosityOption.CreateOption());
        this.Options.Add(QuietOption.CreateOption());
        this.Options.Add(new InfoOption(open));
        this.Subcommands.Add(new BuildCommand(open));
        this.Subcommands.Add(new CleanCommand(open));
        this.Subcommands.Add(new ServeCommand(open));
    }

    /// <summary>
    ///     Creates the root command with every option and subcommand the tool declares.
    /// </summary>
    /// <param name="open">
    ///     The factory that opens the session of a parsed command line.
    /// </param>
    /// <returns>
    ///     The root command, with every subcommand and option the tool declares.
    /// </returns>
    public static GelynCommand Create(Func<ParseResult, Session> open)
    {
        ArgumentNullException.ThrowIfNull(open);
        return new GelynCommand(open);
    }
}
