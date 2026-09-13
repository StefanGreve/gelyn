using System.CommandLine;
using System.Diagnostics.CodeAnalysis;

using Gelyn.Internals;

using Spectre.Console;

namespace Gelyn.Commands;

/// <summary>
///     The root command of the <c>gelyn</c> tool.
/// </summary>
[SuppressMessage("Performance", "CA1812", Justification = Justifications.ByDesign)]
internal sealed class GelynCommand : RootCommand
{
    private readonly Option<string> _greetOption = new("--greet", "-g")
    {
        Description = "Name to greet."
    };

    /// <summary>
    ///     Initializes a new instance of the <see cref="GelynCommand"/> class.
    /// </summary>
    /// <param name="console">
    ///     The console the command writes its output to.
    /// </param>
    public GelynCommand(IAnsiConsole console) : base("A static site generator.")
    {
        Options.Add(_greetOption);

        SetAction(parseResult =>
        {
            string name = parseResult.GetValue(_greetOption) ?? "world";
            console.MarkupLineInterpolated($"Hello from [green]gelyn[/], [bold]{name}[/]!");
        });
    }
}
