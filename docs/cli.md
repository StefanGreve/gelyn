---
title: CLI
date: 2026-10-18
revised: 2026-11-12
description: Shell tab completion, the serve command, the request log, diagnostics, and the exit codes a run reports.
enableSyntaxHighlighting: true
---

# CLI

[TOC]

`gelyn build` generates your site, `gelyn serve` previews it while you edit, and `gelyn clean` removes the
output directory. This page covers how to run them, from tab completion to the exit code a run reports. The
flags that set a configuration value are listed under
[Where settings come from](configuration.md#where-settings-come-from).

## Tab completion

Gelyn is built on `System.CommandLine`, so shell completions work through `dotnet-suggest`. Follow
[How to enable tab completion][tab-completion] to set up your shell once. A global install needs nothing
more: `dotnet-suggest` finds every tool in `~/.dotnet/tools` by itself, so completion works as soon as
`dotnet tool install --global Gelyn` finishes.

An executable anywhere else, such as one installed with `--tool-path` or published from source, has to be
registered once by its path:

```sh
dotnet-suggest register --command-path /path/to/gelyn
```

## serve

`gelyn serve` builds the site, serves it over HTTP on loopback, and builds it again whenever a source file or
`gelyn.json` changes. It runs until you interrupt it:

```sh
gelyn serve                  # http://127.0.0.1:9624
gelyn serve --port 8080      # another port
gelyn serve --launch-browser # open the address in your default browser
gelyn serve --refresh 2      # reload the page in the browser every two seconds
```

| Option             | Default | Description                                         |
| ------------------ | ------- | --------------------------------------------------- |
| `--port`           | `9624`  | Overrides `Port`, the loopback port to serve on.    |
| `--launch-browser` | off     | Opens the served address in the default browser.    |
| `--refresh`        | off     | Reloads an HTML page every so many seconds, from 1. |

Both `http://127.0.0.1:<port>` and `http://localhost:<port>` are served, so either spelling reaches the same
site. Every `build` option applies unchanged, because serving implies building. `Ctrl+C` stops the server and
exits with code `0`: interrupting it is how the command is meant to end.

What the rebuild loop does with a change, and what it does with one that fails to build, is covered under
[Preview](usage.md#preview).

## Request log

Each answered request is written to standard output in [Common Log Format][clf], the shape
`python3 -m http.server` prints, under the line that announces the address:

```text
Serving HTTP on 127.0.0.1 port 9624 (http://127.0.0.1:9624/) ...
127.0.0.1 - - [18/Oct/2026:18:35:26 +0200] "GET / HTTP/1.1" 200 1385
```

The two `-` fields are the identity and the authenticated user, neither of which a preview server has.

## Diagnostics

Gelyn writes results to standard output and diagnostics to standard error, so you can separate the two. A
normal build writes nothing to standard error. Under `serve` the result takes a second shape, the request log
above being standard output as well, so `gelyn serve 2>/dev/null` keeps the log and drops the diagnostics
beside it. Raise `--verbosity` to `debug`, and every phase of the build reports how long it took:

```sh
gelyn build --verbosity debug             # phase timings alongside the result
gelyn build --verbosity debug 2>/dev/null # result only
gelyn build > pages.txt 2> timings.txt    # each stream to its own file
gelyn serve --verbosity debug             # the request log, plus what each rebuild did
```

A build of the example site reports its phases like this:

```text
02:10:11.991 dbug: Gelyn.Program[1006] Started in 11.06 ms
02:10:12.014 dbug: Gelyn.Services.SiteBuilder[1000] Scanned 20 source file(s) in 0.71 ms
02:10:12.014 dbug: Gelyn.Services.SiteBuilder[1001] Rendered 17 page(s) in 1.95 ms
02:10:12.014 dbug: Gelyn.Services.SiteBuilder[1002] Composed 17 page(s) in 0.22 ms
02:10:12.014 dbug: Gelyn.Services.SiteBuilder[1003] Wrote 22 output file(s) in 16.24 ms
02:10:12.014 dbug: Gelyn.Services.SiteBuilder[1004] Removed 0 file(s) in 0.98 ms
02:10:12.014 dbug: Gelyn.Services.SiteBuilder[1011] Allocated 2.1 MB, paused 0.00 ms for 0/0/0 collection(s)
02:10:12.014 dbug: Gelyn.Services.SiteBuilder[1005] Processed 22 file(s) in 20.10 ms
```

More output files are written than source files scanned, because the feed and the sitemap have no source of
their own, and the total adds what was removed to what was written.

Scanning, rendering and removing are measured on their own. Composing and writing are not, because Gelyn
composes and writes pages on several threads at once, and a page is being written while others are still
being composed. The two phases therefore share the time of that pass in proportion to the thread time each
took, which keeps the phases adding up to the total that the last line reports.

The line before the total says what the build allocated on the managed heap, how long the garbage collector
paused it, and how many collections of generation 0, 1 and 2 ran meanwhile. A collection of a higher
generation collects the lower ones too, so the three counts never increase from left to right. The pause is
the figure to watch: as long as it stays a small fraction of the rendering time, allocation is not what the
build waits on.

`--verbosity` accepts any `LogLevel` name: `trace`, `debug`, `information`, `warning`, `error`, `critical`, or
`none`. It defaults to `warning`, which is why a normal build stays quiet.

`gelyn --info` prints build, platform and configuration information to paste into a bug report. It runs even
when the configuration file doesn't load. Paths below your home directory start with `~`, so the report doesn't
put your user name into a public issue.

The `AppSettings` line names the configuration file the run reads. A `--config` path takes precedence when it
exists and ends in `.json`; otherwise the `gelyn.json` of the working directory applies. The line reads
`(undefined)` when neither is present, which is the case Gelyn builds with its defaults:

```sh
gelyn --info                      # AppSettings: ~/site/gelyn.json
gelyn --info --config other.json  # AppSettings: ~/site/other.json
gelyn --info                      # AppSettings: (undefined), run where no gelyn.json is
```

## Exit codes

| Code | Meaning                                                    |
| ---- | ---------------------------------------------------------- |
| `0`  | The command succeeded.                                     |
| `1`  | The command failed.                                        |
| `2`  | Gelyn rejected the configuration before the build started. |

Code `1` also covers a command line that Gelyn can't parse, because that's what `System.CommandLine` returns
for one, so you can't tell an unrecognized option from a failed build. Code `2` means a setting is unusable,
such as a `BaseUrl` that isn't an absolute `http` or `https` URL, a `--config` path that doesn't exist, or a
content directory without an `index.md`. Every command also returns `2` for a configuration file that doesn't
parse. `--help`, `--version` and `--info` return `0` even when the configuration file doesn't load.

`serve` returns `0` when you interrupt it, `2` when it rejects the configuration as `build` would, and `1`
when the port is already taken, when the platform carries no usable `HttpListener`, or when the first build
fails. A rebuild that fails later keeps the server up and changes no exit code, and so does a request the
server can't answer, which gets a `500` and a report on standard error instead.

<!-- References -->

[clf]: https://en.wikipedia.org/wiki/Common_Log_Format
[tab-completion]: https://learn.microsoft.com/en-us/dotnet/standard/commandline/how-to-enable-tab-completion
