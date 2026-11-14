# Gelyn

Gelyn is a deliberately opinionated SSG that cares about semantically correct HTML output. The HTML skeleton is
fixed by design, with no plugin system or template engine. Pages can be customized through CSS, which is the
only supported personalization route. This tool is primarily built for people who care more about content
creation than about an abundance of customization options. If you need more control over the generated output
and don't shy away from meddling with web development, I can recommend using [11ty](https://www.11ty.dev)
instead.

## Install

```sh
dotnet tool install -g gelyn
```

Installing requires the .NET 10 SDK or later. The tool itself is compiled to a native executable for macOS
(arm64, x64), Linux (x64, arm64), and Windows (x64, arm64), so it runs without a .NET runtime.

## Documentation

The documentation is published at [stefangreve.github.io/gelyn](https://stefangreve.github.io/gelyn/). It covers
the content tree, the configuration, and the command line. The source code lives on
[GitHub](https://github.com/StefanGreve/gelyn).
