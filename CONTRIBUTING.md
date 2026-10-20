# Contributing

Gelyn is still in a pre-release stage in which the command surface, the configuration schema, and the
generated HTML all change without notice, so outside contributions are not being accepted yet. This will
change once the first stable release ships.

## Theme Development

- Themes live in `plugins/gelyn/skills/bootstrap/themes`, which is their single source of truth.
- To preview one, copy it into `docs/` or `example/` and build that site.
- Edit the copy freely; `docs/*.css` and `example/*.css` are git-ignored, so the preview never lands in a
  commit.
- Copy the finished stylesheet back into `themes/` and push that file alone.
