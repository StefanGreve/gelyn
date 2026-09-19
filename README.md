# Gelyn

[![Build][build-badge]][build-workflow]
[![CodeQL][codeql-badge]][codeql-workflow]
[![.NET][dotnet-badge]][dotnet-download]
[![Platform][platform-badge]][known-rids]
[![License][license-badge]][license]

A simple, cross-platform, and high-performance static site generator for Markdown.

## Usage

Write Markdown into `content/`, then generate the site:

```sh
gelyn build
```

## Tab completion

Built on `System.CommandLine`, so shell completions work through `dotnet-suggest`. Follow
[How to enable tab completion][tab-completion] for the one-time per-machine setup, then register the
installed executable:

```sh
dotnet-suggest register --command-path "$HOME/.dotnet/tools/gelyn"
```

## Developer Notes

Install the development dependencies and register the `git` hooks:

```sh
dotnet tool restore
dotnet tool run husky install
```

Build and run this project:

```sh
dotnet build
dotnet run --project src/Gelyn -- <command>     # e.g. build
```

Run the test suite:

```sh
dotnet test
```

Tests use [`TUnit`][tunit], which runs on [`Microsoft.Testing.Platform`][mtp].
A [`Husky.Net`][husky] `pre-push` hook runs the same suite before every push.

To use the tool locally, publish it and link the resulting binary onto your `PATH`:

```sh
dotnet publish src/Gelyn -r osx-arm64
ln -sf "$PWD/src/Gelyn/bin/Release/net10.0/osx-arm64/publish/gelyn" ~/.local/bin/gelyn
```

`PublishAot` makes that a standalone executable, so the link is made once and every later `dotnet publish`
updates the command in place. Then invoke it by its command name (`gelyn`).

> [!TIP]
> Use one of the known [RIDs][known-rids] in place of `osx-arm64`; `Gelyn.csproj` lists the set this project
> targets. Native AOT cannot cross-compile between operating systems, so each one is built on its own.

Packing and installing as a global tool works differently: it rehearses what a consumer gets, including
the runtime-specific package layout that the publish output alone does not cover.

```sh
dotnet pack && dotnet pack -r osx-arm64
dotnet tool install --global Gelyn --prerelease
```

> [!NOTE]
> A plain `dotnet pack` emits only the pointer package listing the per-architecture ones, so the matching RID
> has to be packed as well. `Gelyn` resolves as a package id rather than a path, so `nuget.config` declares
> `artifacts/nupkg` as the `local` source and maps `Gelyn*` to it.

<!-- References -->

[build-badge]: https://github.com/StefanGreve/gelyn/actions/workflows/dotnet-build.yml/badge.svg
[build-workflow]: https://github.com/StefanGreve/gelyn/actions/workflows/dotnet-build.yml
[codeql-badge]: https://github.com/StefanGreve/gelyn/actions/workflows/codeql.yml/badge.svg
[codeql-workflow]: https://github.com/StefanGreve/gelyn/actions/workflows/codeql.yml
[dotnet-badge]: https://img.shields.io/badge/.NET-10.0-512BD4
[dotnet-download]: https://dotnet.microsoft.com/en-us/download/dotnet/10.0
[license-badge]: https://img.shields.io/github/license/StefanGreve/gelyn?color=green
[platform-badge]: https://img.shields.io/badge/platform-macOS%20%7C%20Linux%20%7C%20Windows-lightgrey
[license]: LICENSE.md
[tunit]: https://tunit.dev
[husky]: https://alirezanet.github.io/Husky.Net
[mtp]: https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-intro
[tab-completion]: https://learn.microsoft.com/en-us/dotnet/standard/commandline/how-to-enable-tab-completion
[known-rids]: https://learn.microsoft.com/en-us/dotnet/core/rid-catalog?source=recommendations#known-rids
