# Gelyn

A static site generator, packaged as a .NET tool.

## Build

```sh
dotnet build
```

## Run

```sh
dotnet run --project src/Gelyn
```

## Install locally

Pack the tool and install it from the local package output:

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
