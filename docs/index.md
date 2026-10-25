---
title: Gelyn
date: 2026-10-20
description: A static site generator that turns a folder of Markdown into a website.
---

# Gelyn

Gelyn turns a folder of Markdown into a website. The name and the position of each file decide what it
becomes, so you write no templates and maintain no manifest.

```sh
gelyn build
```

No `--config` is needed, because `gelyn` reads `gelyn.json` from the working directory, where
`ContentDirectory` defaults to `content/`.

Preview the result, rebuilt as you write:

```sh
gelyn serve --refresh 2 --launch-browser
```

> [!NOTE]
> Serves the site at <http://127.0.0.1:9624> on loopback alone, for local development only, and generates it
> again whenever a source file or `gelyn.json` changes. `--port` moves it, `--refresh <seconds>` has the
> browser reload the page for you, and <http://localhost:9624> is served as well.

The [repository][repository] carries a complete site under `example/`. Generate it from the repository root,
which is what `example/gelyn.json` resolves its relative paths against:

```sh
gelyn build --config ./example/gelyn.json
```

[Usage](usage.md) is the place to start; the banner links the rest.

<!-- References -->

[repository]: https://github.com/StefanGreve/gelyn
