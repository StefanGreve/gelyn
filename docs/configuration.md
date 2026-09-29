# Configuration

Optional. Without a configuration file the defaults below apply. To change them, drop a `gelyn.json` next to
`content/` in the directory you run `gelyn` from:

```json
{
  "$schema": "https://raw.githubusercontent.com/StefanGreve/gelyn/master/gelyn.schema.json",
  "Title": "My Blog",
  "Language": "en",
  "Author": "Stefan Greve",
  "ContentDirectory": "content",
  "OutputDirectory": "_site"
}
```

`Language` is written to the `lang` attribute of every document. `Author` and `Description` are written to the
matching `meta` tags and are omitted when unset. A page can override `Description` in its front matter:

```markdown
---
title: About
date: 2026-09-14
description: Who I am and what I write about
---
```

Referencing the schema gives completion and validation in any editor that understands JSON Schema, and rejects
misspelled keys, which configuration binding would otherwise ignore in silence.

Pass `--config <path>` to read a different file; unlike `gelyn.json`, a path given explicitly must exist. Every
setting can also be supplied as an environment variable of the same name, which takes precedence over the file:

```sh
Title="My Blog" gelyn build
gelyn build --config config/production.json
```
