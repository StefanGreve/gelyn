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
  "OutputDirectory": "_site",
  "ExcludePatterns": ["*.tmp", "drafts"]
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
| `ExcludePatterns`  | `[]`      | Names to skip while walking the content tree. See [Exclude patterns](#exclude-patterns).             |

The content root is the working directory, unless the `DOTNET_CONTENTROOT` environment variable points
somewhere else. There is no flag for it: no command declares `--contentRoot`, so passing it is rejected as an
unrecognized argument.

`OutputDirectory` must not resolve to `ContentDirectory` or to a directory inside it, because the build would
then publish its own output as content. A sibling is fine, including one whose name merely begins the same way,
such as `content-old`. Both `--output content/dist` and `--content .` are therefore rejected before the build
starts, with exit code 2.

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

## Exclude patterns

Every file beside the Markdown is copied to the output directory, so anything the build should not publish has
to be named in `ExcludePatterns`. Nothing is excluded by default. This is the set a project is expected to
start from, and the one `gelyn.json` in this repository carries:

```json
"ExcludePatterns": [
  ".git", "node_modules", "bin", "obj", ".DS_Store",
  "Thumbs.db", "*~", "*.swp", "*.orig", "*.html"
]
```

> [!IMPORTANT]
> `*.html` is the entry that matters most. HTML is not an authoring format here, and the copy runs after the
> render, so without this pattern an `about.html` beside `about.md` is published over the page generated from
> the Markdown. Keep it unless the content tree is known to hold no HTML.

Symbolic links are the one exception: they are always skipped, for both files and directories, and no setting
re-enables them. A link lets the walk leave the content directory, one resolving to an ancestor makes the walk
unbounded, and a copy would publish the bytes of its target rather than the link.

Patterns are added to with the flag:

```sh
gelyn build --exclude '*.xcf,drafts'
```

Each pattern is matched against the name of a single file or directory, not against its path, with `*` and `?`
as the only wildcards; `**` is not supported. Matching ignores case, so `.ds_store` is skipped on every
platform. A directory that matches is not descended into, so excluding `drafts` skips everything below it.

> [!NOTE]
> `--exclude` is the one flag that **adds** to the configured value instead of replacing it, because the two
> are combined by index rather than overwritten. Both the file's patterns and the flag's therefore apply.
> It is also the only flag taking several values in one token, which is why they are comma separated: the
> command line layer promotes one flag to one string.

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
defaults are usable on their own. Five settings have a flag:

| Setting            | Flag                     |
| ------------------ | ------------------------ |
| `Title`            | `--title <name>`         |
| `BaseUrl`          | `--base-url <url>`       |
| `ContentDirectory` | `--content <directory>`  |
| `OutputDirectory`  | `--output <directory>`   |
| `ExcludePatterns`  | `--exclude <patterns>`   |

`Language`, `Author` and `Description` have none, so they are set through the file or the environment.

`ExcludePatterns` is the only collection-valued setting, which makes layer 3 awkward for it: an environment
variable named exactly like the setting cannot carry a list, so each entry needs its own
`ExcludePatterns__0`, `ExcludePatterns__1` and so on. The file and `--exclude` are the practical routes.

Referencing the schema gives completion and validation in any editor that understands JSON Schema, and rejects
misspelled keys, which configuration binding would otherwise ignore in silence.
