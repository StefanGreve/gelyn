# How to Contribute

Gelyn is still in a pre-release stage in which the command surface, the configuration schema, and the
generated HTML all change without notice, so outside contributions are not being accepted yet. This will
change once the first stable release ships. The guidelines below describe how issues and pull requests are
handled from then on.

## Reporting an Issue

- Search the [issue list][issues] first, so that a bug which is already reported or fixed does not get a
  second issue.
- Give the issue a clear title and description, the Gelyn version, the platform, and as much of the
  configuration and the content that triggers it as you can share. A page that reproduces the problem on its
  own is the most useful attachment.

## Submitting a Pull Request

- Look through the open [pull requests][pulls] and [issues][issues] before you start, in case the change has
  been discussed already or is implemented but not yet released.
- Address one concern per pull request. A bug fix, a rename, and a dependency bump are three pull requests,
  not one, so that each can be reviewed and reverted on its own.
- Include tests for every behavior the change affects.
- Discuss a change that breaks existing sites, such as a renamed setting or different generated HTML, under
  [Ideas][discussions-ideas] before you write it. Versions follow [Semantic Versioning][semver], so a breaking
  change may be held back until the next release that can carry it.

## Contribution Etiquette

- Every commit has to carry a verified signature. GitHub accepts GPG, SSH, and S/MIME; [About commit
  signature verification][commit-signing] covers generating a key and registering it with your account.
- `make setup` enables signing and installs the git hooks that format staged C# before a commit and run the
  tests before a push. Do not bypass them with `--no-verify`; CI verifies both.
- [Development][docs-dev] describes the toolchain, the build, and the test suite.
- Record every new feature and every bug fix in [`CHANGELOG.md`][changelog] once the first release has
  shipped. Until then the file carries its template alone, a pre-release changing without notice.
- Versions follow [Semantic Versioning][semver], with the minor number carrying the breaking changes until
  1.0.0 ships. No project file declares a version; each one is derived from a signed, `v`-prefixed git tag,
  so no pull request changes one. [Development][docs-dev] covers how a release is cut.

## Theme Development

- Themes live in `plugins/gelyn/skills/bootstrap/themes`, their single source of truth.
- The build embeds that folder in the tool, and `example/` and `docs/` name their theme in the `Theme` setting of
  their `gelyn.json`. An edit therefore shows after the next `dotnet build` or `make gelyn`. Preview another theme
  by passing `--theme` to `gelyn build` or `gelyn serve`.
- A theme is `<name>.css` with an optional `<name>.js`. The build embeds every `.css` and `.js` file in the folder
  and takes its base name as the name of a theme, so each distinct base name becomes a value of `--theme`. A new
  theme therefore also needs its name in the `enum` of `Theme` in `gelyn.schema.json`, which a test compares with
  the bundled names.
- The folder holds theme files alone. A stylesheet that serves one site or one page, such as
  `docs/assets/modern404.css`, belongs in that site. In the folder it would become a theme of its own and fail
  that test.
- For a live preview of an edit under `gelyn serve`, copy the theme's file into the content directory of the site
  that applies the theme, such as `docs/modern.css`. The copy replaces the bundled file while it exists, and every
  build warns about it. Git ignores the copy, so it never reaches a commit.
- Copy the finished stylesheet back into `themes/` and push that file alone.

<!-- References -->

[changelog]: CHANGELOG.md
[commit-signing]: https://docs.github.com/en/authentication/managing-commit-signature-verification/about-commit-signature-verification
[discussions-ideas]: https://github.com/StefanGreve/gelyn/discussions/categories/ideas
[docs-dev]: https://stefangreve.github.io/gelyn/dev.html
[issues]: https://github.com/StefanGreve/gelyn/issues
[pulls]: https://github.com/StefanGreve/gelyn/pulls
[semver]: https://semver.org/spec/v2.0.0.html
