# Contributing

Gelyn is still in a pre-release stage in which the command surface, the configuration schema, and the
generated HTML all change without notice, so outside contributions are not being accepted yet. This will
change once the first stable release ships.

## Before You Contribute

- Every commit has to carry a verified signature. GitHub accepts GPG, SSH, and S/MIME; [About commit
  signature verification][commit-signing] covers generating a key and registering it with your account.
- `make setup` enables signing and installs the git hooks that format staged C# before a commit and run the
  tests before a push. Do not bypass them with `--no-verify`; CI verifies both.
- [Development][docs-dev] describes the toolchain, the build, and the test suite.
- Record every new feature and every bug fix in [`CHANGELOG.md`][changelog] once the first release has
  shipped. Until then the file carries its template alone, a pre-release changing without notice.

## Theme Development

- Themes live in `plugins/gelyn/skills/bootstrap/themes`, their single source of truth.
- `make setup` copies `classic.css` into `example/` and `modern.css` into `docs/`, which those two sites
  build with. Preview another theme by copying it into either directory and building that site.
- Edit the copy freely; `docs/*.css` and `example/*.css` are git-ignored, so it never reaches a commit.
- Copy the finished stylesheet back into `themes/` and push that file alone.

<!-- References -->

[changelog]: CHANGELOG.md
[commit-signing]: https://docs.github.com/en/authentication/managing-commit-signature-verification/about-commit-signature-verification
[docs-dev]: https://stefangreve.github.io/gelyn/dev.html
