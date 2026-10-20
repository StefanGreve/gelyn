---
title: Configuration
date: 2026-10-15
description: Every setting gelyn.json accepts, its default, and the order the sources are read in.
---

# Configuration

Gelyn runs without configuration, so you only need a file to change a default. To change one, create a
`gelyn.json` beside `content/` in the directory you run `gelyn` from:

```json
{
  "$schema": "https://stefangreve.github.io/gelyn/schema.json",
  "Title": "My Blog",
  "Language": "en",
  "Author": "Stefan Greve",
  "Description": "Notes on software engineering.",
  "BaseUrl": "https://example.com/blog",
  "KaTeXVersion": "0.19.0",
  "MermaidVersion": "12.1.0",
  "ContentDirectory": "content",
  "OutputDirectory": "_site",
  "ExcludePatterns": ["*.tmp", "drafts"]
}
```

Reference [the schema](https://stefangreve.github.io/gelyn/schema.json) to get completion and validation in
any editor that understands JSON Schema. It also rejects a misspelled key, which configuration binding would
otherwise ignore in silence.

## Settings

| Setting            | Default   | Description                                                                                             |
| ------------------ | --------- | ------------------------------------------------------------------------------------------------------- |
| `Title`            | `Gelyn`   | Names your site. The header shows it as a link back to the landing page.                                |
| `Language`         | `en`      | Sets the `lang` attribute of every document. Use a BCP 47 tag.                                          |
| `Author`           | unset     | Fills the `author` meta tag. While unset, Gelyn omits the tag.                                          |
| `Description`      | unset     | Fills the `description` meta tag of any page whose front matter declares none.                          |
| `BaseUrl`          | unset     | Sets the absolute URL your site is served from. See [Base URL](#base-url).                              |
| `KaTeXVersion`     | `0.19.0`  | Pins the KaTeX release a page loads when its front matter enables math rendering.                       |
| `MermaidVersion`   | `12.1.0`  | Pins the Mermaid release a page loads when its front matter enables diagram rendering.                  |
| `ContentDirectory` | `content` | Points at the directory Gelyn builds from, Markdown and copies alike.                                   |
| `OutputDirectory`  | `_site`   | Points at the directory Gelyn writes the site to, overwriting the files already there.                  |
| `ExcludePatterns`  | `[]`      | Lists the names Gelyn skips while walking your content tree. See [Exclude patterns](#exclude-patterns). |

Gelyn resolves both directories against the content root, which is the directory you run `gelyn` from unless
the `DOTNET_CONTENTROOT` environment variable points somewhere else. No command declares a `--contentRoot`
flag, so passing one fails as an unrecognized argument.

`OutputDirectory` and `ContentDirectory` can't contain each other. An output directory inside your content
directory becomes content in the next build, and a content directory inside your output directory disappears
when you run `gelyn clean`. A sibling is fine, including one whose name merely begins the same way, such as
`content-old`. Gelyn therefore rejects `--output content/dist`, `--output .`, and `--content .` before the
command starts, with exit code 2.

A page title is separate from the name of your site. It comes from the page's own front matter, falling back to
the file name, or to the folder name for a section index. Gelyn never uses `Title` as a page title.

A page can also override `Description` in its front matter:

```markdown
---
title: About
date: 2026-09-14
description: Who I am and what I write about
---
```

Gelyn writes `date` to the `revised` meta tag and omits the tag when the front matter declares none.

## Base URL

`BaseUrl` is the absolute URL your finished site is served from. Gelyn prefixes its path to every generated
link, so a site served from below the root of a domain keeps working, and uses its origin to give every page a
canonical address:

```sh
gelyn build --base-url https://example.com/blog
```

```html
<link rel="canonical" href="https://example.com/blog/about.html">
<a href="/blog/about.html">About</a>
```

A trailing slash makes no difference, and a URL that addresses the root of a domain contributes no prefix at
all, so `https://example.com` leaves every link exactly as it would be with no `BaseUrl` configured. Gelyn
accepts only absolute `http` and `https` URLs, and rejects anything else before the build starts. While
`BaseUrl` is unset, Gelyn writes no canonical reference at all, because a relative one would carry no meaning.

## Exclude patterns

Gelyn copies every file beside your Markdown to the output directory, so name anything the build shouldn't
publish in `ExcludePatterns`. Nothing is excluded by default. This is the set to start a project from:

```json
"ExcludePatterns": [
  ".git", "node_modules", "bin", "obj", ".DS_Store",
  "Thumbs.db", "*~", "*.swp", "*.orig", "*.html"
]
```

> [!IMPORTANT]
> `*.html` is the entry that matters most. HTML isn't an authoring format here, and the copy runs after the
> render, so without this pattern an `about.html` beside `about.md` is published over the page generated from
> the Markdown. Keep it unless you know your content tree holds no HTML.

Name your configuration file too if it sits inside the content directory. Gelyn reads `gelyn.json` before the
scan and has no further knowledge of it, so a config file among your Markdown is copied like any other file.
This repository does exactly that: `example/gelyn.json` configures the tree it sits in, and so carries
`gelyn.json` as an eleventh pattern.

Symbolic links are the one exception. Gelyn always skips them, for both files and directories, and no setting
re-enables them. A link lets the walk leave your content directory, one that resolves to an ancestor makes the
walk unbounded, and a copy would publish the bytes of the target rather than the link.

Add to your patterns with the flag:

```sh
gelyn build --exclude '*.xcf,drafts'
```

Gelyn matches each pattern against the name of a single file or directory, not against its path, with `*` and
`?` as the only wildcards. `**` isn't supported. Matching ignores case, so `.ds_store` is skipped on every
platform. Gelyn doesn't descend into a directory that matches, so excluding `drafts` skips everything below it.

> [!NOTE]
> `--exclude` is the one flag that **adds** to the configured value instead of replacing it, because Gelyn
> combines the two by index rather than overwriting. Both the file's patterns and the flag's apply. It's also
> the only flag that takes several values in one token, which is why you separate them with commas: the
> command line layer promotes one flag to one string.

## Where settings come from

Gelyn reads your settings from four layers, each overriding the one before it:

1. The built-in defaults in [Settings](#settings).
2. `gelyn.json` in the working directory, or the file you pass to `--config`.
3. An environment variable named exactly like the setting.
4. A command line flag.

```sh
Title="My Blog" gelyn build                  # environment variable
gelyn build --config config/production.json  # a different file
gelyn build --title "My Blog"                # flag, which wins over both of the others
```

A path you pass to `--config` must exist, unlike `gelyn.json`, which stays optional because the defaults are
usable on their own. Five settings have a flag:

| Setting            | Flag                     |
| ------------------ | ------------------------ |
| `Title`            | `--title <name>`         |
| `BaseUrl`          | `--base-url <url>`       |
| `ContentDirectory` | `--content <directory>`  |
| `OutputDirectory`  | `--output <directory>`   |
| `ExcludePatterns`  | `--exclude <patterns>`   |

`Language`, `Author`, `Description`, `KaTeXVersion`, and `MermaidVersion` have none, so set them in the file
or in the environment.

`ExcludePatterns` is the only collection-valued setting, which makes layer 3 awkward for it. An environment
variable named exactly like the setting can't carry a list, so each entry needs its own `ExcludePatterns__0`,
`ExcludePatterns__1`, and so on. The file and `--exclude` are the practical routes.
