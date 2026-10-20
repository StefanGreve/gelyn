# Gelyn

[![Build][build-badge]][build-workflow]
[![CodeQL][codeql-badge]][codeql-workflow]
[![Documentation][docs-badge]][docs-site]
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
> This project is a work in progress. Things can break and change at any time. Use at your own risk. Gelyn is
> not published to NuGet yet, so the install command below will not resolve until the first release ships.

## Install

```sh
dotnet tool install -g gelyn
```

Building from source, and linking a local publish onto your `PATH` instead, are covered in
[Development][docs-dev].

## Documentation

The documentation is published at [stefangreve.github.io/gelyn][docs-site], which Gelyn generates from the
`docs/` directory of this repository.

## Claude Code Plugin

If you use [Claude Code][claude-code], a plugin sets the blog up for you rather than leaving you with an
empty directory. Register the marketplace, then install it:

```sh
/plugin marketplace add stefangreve/claude-plugins
/plugin install gelyn@stefangreve-plugins
```

Ask Claude to set up a blog, or invoke the skill directly:

```sh
/gelyn:bootstrap
```

<!-- References -->

[build-badge]: https://github.com/StefanGreve/gelyn/actions/workflows/dotnet-build.yml/badge.svg
[build-workflow]: https://github.com/StefanGreve/gelyn/actions/workflows/dotnet-build.yml
[codeql-badge]: https://github.com/StefanGreve/gelyn/actions/workflows/codeql.yml/badge.svg
[codeql-workflow]: https://github.com/StefanGreve/gelyn/actions/workflows/codeql.yml
[docs-badge]: https://github.com/StefanGreve/gelyn/actions/workflows/gelyn-build.yml/badge.svg
[docs-dev]: https://stefangreve.github.io/gelyn/dev.html
[docs-site]: https://stefangreve.github.io/gelyn/
[dotnet-badge]: https://img.shields.io/badge/.NET-10.0-512BD4
[dotnet-download]: https://dotnet.microsoft.com/en-us/download/dotnet/10.0
[license-badge]: https://img.shields.io/github/license/StefanGreve/gelyn?color=green
[platform-badge]: https://img.shields.io/badge/platform-macOS%20%7C%20Linux%20%7C%20Windows-lightgrey
[license]: LICENSE.md
[11ty]: https://www.11ty.dev
[claude-code]: https://claude.com/claude-code
[marketplace]: https://github.com/StefanGreve/claude-plugins
[known-rids]: https://learn.microsoft.com/en-us/dotnet/core/rid-catalog?source=recommendations#known-rids
