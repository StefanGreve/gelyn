using System.Threading.Tasks;

using Gelyn.Commands;
using Gelyn.Extensions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Gelyn;

/// <summary>
///     Entry point for the <c>gelyn</c> command line tool.
/// </summary>
internal static class Program
{
    /// <summary>
    ///     Runs the tool.
    /// </summary>
    /// <param name="args">
    ///     Command line arguments passed to the tool.
    /// </param>
    /// <returns>
    ///     Zero on success, a non-zero exit code otherwise.
    /// </returns>
    internal static async Task<int> Main(string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddGelyn();

        using IHost host = builder.Build();

        return await host.Services
            .GetRequiredService<GelynCommand>()
            .Parse(args)
            .InvokeAsync()
            .ConfigureAwait(false);
    }
}
