# CLI

## Tab completion

Built on `System.CommandLine`, so shell completions work through `dotnet-suggest`. Follow
[How to enable tab completion][tab-completion] for the one-time per-machine setup, then register the
installed executable:

```sh
dotnet-suggest register --command-path "$HOME/.dotnet/tools/gelyn"
```

## Diagnostics

Results go to standard output and diagnostics go to standard error, so the two can be separated. Nothing is
written to standard error unless `--verbosity` asks for it; `debug` reports how long each phase of the build
took:

```sh
gelyn build --verbosity debug             # phase timings alongside the result
gelyn build --verbosity debug 2>/dev/null # result only
gelyn build > pages.txt 2> timings.txt    # each stream to its own file
```

Accepts any `LogLevel` name - `trace`, `debug`, `information`, `warning`, `error`, `critical` or `none` - and
defaults to `warning`, which is why a normal build stays quiet.

## Exit Codes

| Code | Meaning                                                  |
| ---- | -------------------------------------------------------- |
| `0`  | The command succeeded.                                   |
| `1`  | The command failed.                                      |
| `2`  | The configuration was rejected before the build started. |

`1` also covers a command line that could not be parsed, because that is what `System.CommandLine` returns for
one, so an unrecognised option and a failed build are not distinguishable. `2` is what an unusable setting
produces, such as a `BaseUrl` that is not an absolute `http` or `https` URL, or a `--config` path that does not
exist.

<!-- References -->

[tab-completion]: https://learn.microsoft.com/en-us/dotnet/standard/commandline/how-to-enable-tab-completion
