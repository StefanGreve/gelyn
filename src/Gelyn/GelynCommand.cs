using System.CommandLine;

using Spectre.Console;

namespace Gelyn;

/// <summary>
///     The root command of the <c>gelyn</c> tool.
/// </summary>
internal sealed class GelynCommand : RootCommand
{
    private readonly Option<string> _greetOption = new("--greet", "-g")
    {
        Description = "Name to greet."
    };

    /// <summary>
    ///     Initializes a new instance of the <see cref="GelynCommand"/> class.
    /// </summary>
    public GelynCommand() : base("A static site generator.")
    {
        Options.Add(_greetOption);

        SetAction(parseResult =>
        {
            string name = parseResult.GetValue(_greetOption) ?? "world";
            AnsiConsole.MarkupLineInterpolated($"Hello from [green]gelyn[/], [bold]{name}[/]!");
        });
    }
}