# Gelyn

A static site generator, packaged as a .NET tool.

## Usage

Write Markdown into `content/`, then generate the site:

```sh
gelyn build
```

Output is written to `_site/`. Both directories are resolved relative to the working directory.

## Tab completion

Built on `System.CommandLine`, so shell completions work through `dotnet-suggest`. Follow
[How to enable tab completion][tab-completion] for the one-time per-machine setup, then register the
installed executable:

```sh
dotnet-suggest register --command-path "$HOME/.dotnet/tools/gelyn"
```

## Developer Notes

Build and run this project:

```sh
dotnet build
dotnet run --project src/Gelyn -- <command>     # e.g. substitute <command> with build
```

Run the test suite:

```sh
dotnet test
```

Tests use [`TUnit`][tunit], which runs on [`Microsoft.Testing.Platform`][mtp].

To install this project locally, pack the tool and install it from the local package output:

```sh
dotnet pack
dotnet tool install --global --add-source ./artifacts/nupkg Gelyn --prerelease
```

Then invoke it by its command name:

```sh
gelyn
```

To update after a rebuild, uninstall and reinstall:

```sh
dotnet tool uninstall --global Gelyn
```

<!-- References -->

[tunit]: https://tunit.dev
[mtp]:https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-intro
[tab-completion]: https://learn.microsoft.com/en-us/dotnet/standard/commandline/how-to-enable-tab-completion
