---
title: Gelyn
date: 2026-10-25
description: A static site generator that turns a folder of Markdown into a website.
---

# Getting Started

Gelyn turns a folder of Markdown into a website. The name and the position of each file decide what it
becomes, so you write no templates and maintain no manifest. This page takes you from an empty directory to
a site you can deploy.

## Install

```sh
dotnet tool install -g gelyn
```

> [!WARNING]
> Gelyn is not on NuGet yet, so that command does not resolve until the first release ships. Until then,
> build it from source and link the publish onto your `PATH`, which [Development](dev.md) covers.

## Write a page

A site is a directory of Markdown, and exactly one file is required. Create `content/index.md`:

```markdown
---
title: Home
---

# Hello

My first page.
```

Without that file, Gelyn reports `error: No landing page found at '...'` and exits with code 1 before
writing anything. Everything else is optional: put `about.md` beside it and the banner links it, drop a
`.css` file in and every page in that directory and below it loads it.

> [!TIP]
> If you use Claude Code, the plugin described in the [repository][repository] writes the configuration and
> a starter tree for you. Invoke it with `/gelyn:bootstrap`.

## Configure it

No setting is required. Gelyn runs on its defaults, so the tree above builds with no `gelyn.json` at all,
and you only need the file to change one. For a hello world the single setting worth writing is the title,
because it defaults to `Gelyn` and labels the banner on every page. Put this beside `content/`:

```json
{
  "$schema": "https://stefangreve.github.io/gelyn/schema.json",
  "Title": "My Blog"
}
```

Two more are worth knowing about this early:

| Setting       | Default   | Set it when                                                                 |
| ------------- | --------- | --------------------------------------------------------------------------- |
| `Title`       | `Gelyn`   | Always, since it names your site in the banner and links back to this page. |
| `Description` | unset     | You want a `description` meta tag on pages that declare none themselves.    |
| `BaseUrl`     | unset     | The site is served below the root of a domain, as a GitHub Pages project is. |

The `$schema` line is optional too, and buys completion and validation in any editor that understands JSON
Schema, including a complaint about a key you misspelled. [Configuration](configuration.md) lists the rest.

## Build it

```sh
gelyn build
```

No `--config` is needed, because `gelyn` reads `gelyn.json` from the working directory. The finished site
lands in `_site/` and is ready to deploy as it is, with no build step of its own and nothing to serve it but
a static host.

## Preview it

```sh
gelyn serve --refresh 2 --launch-browser
```

> [!NOTE]
> Serves the site at <http://127.0.0.1:9624> on loopback alone, for local development only, and generates it
> again whenever a source file or `gelyn.json` changes. `--port` moves it, `--refresh <seconds>` has the
> browser reload the page for you, and <http://localhost:9624> is served as well.

## Where to go next

[Usage](usage.md) is the reference for what Gelyn does with a content tree: front matter, page titles, the
navigation, the cascade that links your stylesheets and scripts, and every Markdown extension it renders.
[Configuration](configuration.md) lists every setting and the layers it can come from, and [CLI](cli.md)
covers the options, the request log and the exit codes a run reports.

The [repository][repository] also carries a complete site under `example/` to read through. Generate it from
the repository root, which is what `example/gelyn.json` resolves its relative paths against:

```sh
gelyn build --config ./example/gelyn.json
```

<!-- References -->

[repository]: https://github.com/StefanGreve/gelyn
