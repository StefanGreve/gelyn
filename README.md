# Gelyn

A static site generator, packaged as a .NET tool.

## Usage

TODO

## Tab completion

Built on `System.CommandLine`, so shell completions work through `dotnet-suggest`. Follow
[How to enable tab completion][tab-completion] for the one-time per-machine setup, then register the
installed executable:

```sh
dotnet-suggest register --command-path "$HOME/.dotnet/tools/gelyn"
```

[tab-completion]: https://learn.microsoft.com/en-us/dotnet/standard/commandline/how-to-enable-tab-completion

## Developer Notes

Build and run this project:

```sh
dotnet build
dotnet run --project src/Gelyn
```

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
