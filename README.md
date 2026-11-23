<div align="center">

<p>
<a name="gelyn"></a>
<picture>
  <source media="(prefers-color-scheme: dark)" srcset="assets/gelyn-white.svg">
  <img src="assets/gelyn-black.svg" alt="Gelyn" height="96">
</picture>
</p>

*A simple, cross-platform, and high-performance static site generator (SSG) for Markdown.*

[![Build][build-badge]][build-workflow]
[![CodeQL][codeql-badge]][codeql-workflow]
[![Documentation][docs-badge]][docs-site]
[![.NET][dotnet-badge]][dotnet-download]
[![Platform][platform-badge]][known-rids]
[![License][license-badge]][license]
</div>

Gelyn is a deliberately opinionated SSG that cares about semantically correct HTML output. The HTML
skeleton is fixed by design, with no plugin system or template engine. Pages can be customized through CSS
and JavaScript, which are the only supported personalization routes. This tool is primarily built for
people who care more about content creation than about an abundance of customization options. If you need
more control over the generated output and don't shy away from meddling with web development, I can
recommend using [11ty][11ty] instead.

> [!WARNING]
> This project is a work in progress. Things can break and change at any time. Use at your own risk. Gelyn is
> not published to NuGet yet, so the install command below will not resolve until the first release ships.

## Installation

```sh
dotnet tool install -g gelyn
```

Building from source, and linking a local publish onto your `PATH` instead, are covered in
[Development][docs-dev].

## Features

Besides converting Markdown to HTML, Gelyn ships with the following features out of the box:

- [Markdown extensions][docs-markdown] beyond CommonMark, such as alert blocks, footnotes, figures, task lists,
  and `[TOC]` / `[TOD]` markers for a table of contents or a table of documents
- [LaTeX math][docs-math] through KaTeX, [Mermaid diagrams][docs-diagrams], and
  [syntax highlighting][docs-highlighting] through Shiki, each enabled per page with one front matter key
- [RSS feed][docs-feed] and [sitemap][docs-sitemap] generation
- [Bundled themes][docs-themes] and optional [giscus comments][docs-comments]
- [`gelyn serve`][docs-serve], a local preview server that rebuilds on every change

The documentation is published at [stefangreve.github.io/gelyn][docs-site], which Gelyn generates from the
`docs/` directory of this repository.

## Claude Code Plugin

If you use [Claude Code][claude-code], a plugin sets the blog up for you rather than leaving you with an
empty directory. Register the [marketplace], then install it:

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
[docs-markdown]: https://stefangreve.github.io/gelyn/usage.html#markdown
[docs-math]: https://stefangreve.github.io/gelyn/usage.html#mathematics
[docs-diagrams]: https://stefangreve.github.io/gelyn/usage.html#diagrams
[docs-highlighting]: https://stefangreve.github.io/gelyn/usage.html#syntax-highlighting
[docs-feed]: https://stefangreve.github.io/gelyn/configuration.html#feed
[docs-sitemap]: https://stefangreve.github.io/gelyn/configuration.html#sitemap
[docs-themes]: https://stefangreve.github.io/gelyn/configuration.html#themes
[docs-comments]: https://stefangreve.github.io/gelyn/usage.html#comments
[docs-serve]: https://stefangreve.github.io/gelyn/cli.html#serve
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
