# Configuration

Optional. Without a configuration file the defaults below apply. To change them, drop a `gelyn.json` next to
`content/` in the directory you run `gelyn` from:

```json
{
  "$schema": "https://raw.githubusercontent.com/StefanGreve/gelyn/master/gelyn.schema.json",
  "Title": "My Blog",
  "Language": "en",
  "Author": "Stefan Greve",
  "Description": "Notes on software engineering.",
  "BaseUrl": "https://example.com/blog",
  "ContentDirectory": "content",
  "OutputDirectory": "_site"
}
```

## Settings

| Setting            | Default   | Effect                                                                                               |
| ------------------ | --------- | ---------------------------------------------------------------------------------------------------- |
| `Title`            | `Gelyn`   | The name of the site, shown in the header as a link back to the landing page.                        |
| `Language`         | `en`      | Written to the `lang` attribute of every document, as a BCP 47 tag.                                  |
| `Author`           | unset     | Written to the `author` meta tag. Omitted from the document while unset.                             |
| `Description`      | unset     | Written to the `description` meta tag of any page whose front matter declares none.                  |
| `BaseUrl`          | unset     | The absolute URL the site is served from. See [Base URL](#base-url).                                 |
| `ContentDirectory` | `content` | The directory holding the Markdown sources. A relative value resolves against the content root.      |
| `OutputDirectory`  | `_site`   | The directory the site is written to. Resolved the same way, and existing files are overwritten.     |

The content root is the working directory, unless the `DOTNET_CONTENTROOT` environment variable points
somewhere else. There is no flag for it: no command declares `--contentRoot`, so passing it is rejected as an
unrecognized argument.

A page title is separate from the name of the site: it comes from the page's own front matter, falling back to
the file name, or to the folder name for a section index. `Title` is never used as a page title.

A page can also override `Description` in its front matter:

```markdown
---
title: About
date: 2026-09-14
description: Who I am and what I write about
---
```

`date` is written to the `revised` meta tag and is omitted when absent.

## Base URL

`BaseUrl` is the absolute URL the finished site is served from. Its path is prefixed to every generated link, so
that a site served from below the root of a domain keeps working, and its origin lets every page state its
canonical address:

```sh
gelyn build --base-url https://example.com/blog
```

```html
<link rel="canonical" href="https://example.com/blog/about.html">
<a href="/blog/about.html">About</a>
```

A trailing slash makes no difference, and a URL addressing the root of a domain contributes no prefix at all, so
`https://example.com` leaves every link exactly as it would be with no `BaseUrl` configured. Only absolute
`http` and `https` URLs are accepted, and anything else is rejected before the build starts. While `BaseUrl` is
unset no canonical reference is written at all, because a relative one would carry no meaning.

## Where settings come from

Four layers, each overriding the one before it:

1. The built-in defaults listed above.
2. `gelyn.json` in the working directory, or the file passed to `--config`.
3. An environment variable named exactly like the setting.
4. A command line flag.

```sh
Title="My Blog" gelyn build                  # environment variable
gelyn build --config config/production.json  # a different file
gelyn build --title "My Blog"                # flag, which wins over both of the above
```

Unlike `gelyn.json`, a path given to `--config` must exist; the conventional file is optional because the
defaults are usable on their own. Four settings have a flag:

| Setting            | Flag                    |
| ------------------ | ----------------------- |
| `Title`            | `--title <name>`        |
| `BaseUrl`          | `--base-url <url>`      |
| `ContentDirectory` | `--content <directory>` |
| `OutputDirectory`  | `--output <directory>`  |

`Language`, `Author` and `Description` have none, so they are set through the file or the environment.

Referencing the schema gives completion and validation in any editor that understands JSON Schema, and rejects
misspelled keys, which configuration binding would otherwise ignore in silence.
