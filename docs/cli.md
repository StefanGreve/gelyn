# CLI

## Tab completion

Gelyn is built on `System.CommandLine`, so shell completions work through `dotnet-suggest`. Follow
[How to enable tab completion][tab-completion] to set up your machine once, then register the installed
executable:

```sh
dotnet-suggest register --command-path "$HOME/.dotnet/tools/gelyn"
```

## Diagnostics

Gelyn writes results to standard output and diagnostics to standard error, so you can separate the two. A
normal build writes nothing to standard error. Raise `--verbosity` to `debug`, and every phase of the build
reports how long it took:

```sh
gelyn build --verbosity debug             # phase timings alongside the result
gelyn build --verbosity debug 2>/dev/null # result only
gelyn build > pages.txt 2> timings.txt    # each stream to its own file
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

<!-- References -->

[tab-completion]: https://learn.microsoft.com/en-us/dotnet/standard/commandline/how-to-enable-tab-completion
