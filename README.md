# Gelyn

[![Build][build-badge]][build-workflow]
[![CodeQL][codeql-badge]][codeql-workflow]
[![.NET][dotnet-badge]][dotnet-download]
[![Platform][platform-badge]][known-rids]
[![License][license-badge]][license]

A simple, cross-platform, and high-performance static site generator (SSG) for Markdown.

Gelyn is a deliberately opinionated SSG that cares about semantically correct HTML output. The HTML
skeleton is fixed by design, with no plugin system or template engine. Pages can be customized through
CSS, which is the only supported personalization route. This tool is primarily built for people who care
more about content creation than about an abundance of customization options. If you need more control
over the generated output and don't shy away from meddling with web development, I can recommend using
[11ty][11ty] instead.

> [!WARNING]
> This project is a work in progress. Things can break and change at any time. Use at your own risk.

## Basic Usage

The `example/` directory holds a complete site. Generate it from the repository root, which is what
`example/gelyn.json` resolves its relative paths against:

```sh
gelyn build --config ./example/gelyn.json
```

Preview the result, rebuilt as you write:

```sh
gelyn serve --config ./example/gelyn.json --launch-browser
```

> [!NOTE]
> Serves the site at <http://127.0.0.1:9624> on loopback alone, for local development only, and generates it
> again whenever a source file or `gelyn.json` changes. `--port` moves it, and <http://localhost:9624> is served as well. Reload the page yourself once a rebuild reports.

Your own site needs no `--config`, because `gelyn` reads `gelyn.json` from the working directory, where
`ContentDirectory` defaults to `content/`.

## Claude Code Plugin

If you use [Claude Code][claude-code], a plugin sets the blog up for you rather than leaving you with an
empty directory. Register the marketplace, then install it:

```sh
claude plugin marketplace add stefangreve/claude-plugins
claude plugin install gelyn@stefangreve-plugins
```

Ask Claude to set up a blog, or invoke the skill directly:

```sh
/gelyn:bootstrap
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

Tests use [`TUnit`][tunit], which runs on [`Microsoft.Testing.Platform`][mtp]; its [documentation][tunit-docs]
covers the authoring conventions this suite follows.
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
[11ty]: https://www.11ty.dev
[claude-code]: https://claude.com/claude-code
[marketplace]: https://github.com/StefanGreve/claude-plugins
[tunit]: https://tunit.dev
[tunit-docs]: https://tunit.dev/docs/intro/
[husky]: https://alirezanet.github.io/Husky.Net
[mtp]: https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-intro
[known-rids]: https://learn.microsoft.com/en-us/dotnet/core/rid-catalog?source=recommendations#known-rids
