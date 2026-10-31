---
name: bootstrap
description: Scaffold a gelyn blog in the current directory, writing gelyn.json from answers the user gives
  and a content tree whose sample post demonstrates the Markdig extensions gelyn enables.
when_to_use: When asked to start, set up, scaffold, or initialize a gelyn site or blog.
model: haiku
effort: medium
---

# Bootstrap a gelyn blog

Stop if `gelyn.json` or `content/` already exists, and say which, unless the user asked to overwrite it.

## 1. Ask

One `AskUserQuestion` call covering `Title`, `Author`, `Language`, and `BaseUrl`. Offer the directory name as
the title and `git config user.name` as the author. `BaseUrl` must be an absolute `http` or `https` URL, so
offer omitting it, which is correct for a site served from the root of a domain.

## 2. Write `gelyn.json`

Omit `Author`, `Description` and `BaseUrl` rather than writing an empty value for one.

```json
{
  "$schema": "https://stefangreve.github.io/gelyn/schema.json",
  "Title": "...",
  "Language": "en",
  "Author": "...",
  "ContentDirectory": "content",
  "OutputDirectory": "_site",
  "ExcludePatterns": [
    ".git", "node_modules", "bin", "obj", ".DS_Store",
    "Thumbs.db", "*~", "*.swp", "*.orig", "*.html"
  ]
}
```

Keep `*.html`: the copy runs after the render, so without it a stale `about.html` is published over the page
generated from `about.md`.

## 3. Ask which theme to use

One `AskUserQuestion` call with three options. Copy the chosen file out of `themes/` to `content/styles.css`,
keeping that name whatever the answer, so a theme can be swapped later without touching anything else.

- `None` - write nothing. Browser defaults apply until the user adds a stylesheet by the rule in step 5.
- `Classic` - `classic.css`, light, and short enough to read end to end. Suggest it to a user who wants to
  style the site themselves, because it gives every extension a plain treatment to edit rather than leaving
  them a blank file to start from.
- `Modern` - `modern.css`, light or dark by browser preference, and ready to use unchanged. Suggest it to a
  user who would rather not write CSS. It covers the common elements, so a definition list, a footnote block
  and a task list fall back to browser defaults. Copy `modern.js` beside it as `content/theme.js`, because
  the stylesheet is half a theme without it: the script builds the sheet menu the stylesheet styles below
  40rem, and gives a Mermaid diagram the appearance the rest of the page follows.

## 4. Copy the templates

`templates/` into `content/`, keeping the tree: `index.md`, `blog/index.md`, `blog/hello.md`. Replace
`{{TITLE}}` and `{{AUTHOR}}`, and `{{DATE}}` with today's date as `YYYY-MM-DD`. The stylesheet is not copied
from here; step 3 writes it.

Navigation follows depth, so `index.md` and `blog/index.md` are linked in the banner and `blog/hello.md` is
not. Keep further posts beside `hello.md` to inherit that.

## 5. Ask about JavaScript, and write none

Ask whether the site will carry its own JavaScript. Create no `.js` file of the user's either way, and leave
the `theme.js` of step 3 where it is. Explain the placement rule instead, so the user writes one where they
want it:

A `.css` or `.js` file is linked from every page in its own directory and in every directory below it.
Placement is the whole mechanism and nothing is declared, so `styles.css` at the content root reaches every
page, while a `blog/blog.css` would reach the posts alone. Links are written outermost first, so a deeper
file overrides a shallower one, and a sibling branch is never reached. A script is written as
`<script defer>` and therefore runs in document order, outermost first.

Several files in one directory load in ordinal name order, which has nothing to do with dependency order:
`app.js` loads before `vendor.js` and breaks if it needs it. Name them so the alphabet agrees, such as
`1-vendor.js` before `2-app.js`, or put the library one directory up. The cascade reaches no CDN, so a
library has to sit in the tree.

## 6. Build

Run `gelyn build` and report the output directory. Add `_site/` to `.gitignore` when the directory is a git
repository.

## Reference

Read these before answering a question about a key or a syntax, and do not substitute a local checkout:

- [`configuration.md`](https://raw.githubusercontent.com/StefanGreve/gelyn/master/docs/configuration.md) -
  every setting, its default, and the layer that overrides it
- [`usage.md`](https://raw.githubusercontent.com/StefanGreve/gelyn/master/docs/usage.md) - front matter keys,
  the full Markdig extension table, navigation, stylesheets, math, diagrams
- [`cli.md`](https://raw.githubusercontent.com/StefanGreve/gelyn/master/docs/cli.md) - commands, flags, exit
  codes

Two traps the extension table understates: a definition's text must start at the fourth column, and
strikethrough is the only emphasis extra enabled, so `==mark==`, `^super^` and `~sub~` render literally.
