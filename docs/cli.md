# CLI

## Tab completion

Gelyn is built on `System.CommandLine`, so shell completions work through `dotnet-suggest`. Follow
[How to enable tab completion][tab-completion] to set up your machine once, then register the installed
executable:

```sh
dotnet-suggest register --command-path "$HOME/.dotnet/tools/gelyn"
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
| `--port`           | `9624`  | The loopback port to serve on, from 1 to 65535.     |
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

`--verbosity` accepts any `LogLevel` name: `trace`, `debug`, `information`, `warning`, `error`, `critical`, or
`none`. It defaults to `warning`, which is why a normal build stays quiet.

## Exit codes

| Code | Meaning                                                    |
| ---- | ---------------------------------------------------------- |
| `0`  | The command succeeded.                                     |
| `1`  | The command failed.                                        |
| `2`  | Gelyn rejected the configuration before the build started. |

Code `1` also covers a command line that Gelyn can't parse, because that's what `System.CommandLine` returns
for one, so you can't tell an unrecognized option from a failed build. Code `2` means a setting is unusable,
such as a `BaseUrl` that isn't an absolute `http` or `https` URL, or a `--config` path that doesn't exist.

`serve` returns `0` when you interrupt it, and `1` when the port is already taken, when the platform carries
no usable `HttpListener`, or when the first build fails. A rebuild that fails later keeps the server up and
changes no exit code.

<!-- References -->

[clf]: https://en.wikipedia.org/wiki/Common_Log_Format
[tab-completion]: https://learn.microsoft.com/en-us/dotnet/standard/commandline/how-to-enable-tab-completion
