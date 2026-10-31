---
title: Tables
date: 2026-10-24
description: Pipe tables, and the table a marker writes for you.
enableFeed: true
---

# Tables

A pipe table follows GitHub rules, so the row of dashes is what makes it a table and the colons in that row
are what align a column:

| Setting            | Default   | Overridden by |
| ------------------ | --------- | ------------- |
| `ContentDirectory` | `content` | `--content`   |
| `OutputDirectory`  | `_site`   | `--output`    |

A cell holding a literal pipe needs escaping, so `a \| b` keeps both halves in one cell.

## Generated tables

The table on the [blog index](index.html) is not written by hand. A `[TOD]` marker is replaced at build time
by a row per page in that directory, plus one per section below it, dated newest first, which is why a new
post needs no link added anywhere.

This post sits beside that index and declares a date, so the table lists it as a file row.
