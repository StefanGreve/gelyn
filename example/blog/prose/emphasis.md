---
title: Emphasis
date: 2026-10-16
description: The one emphasis extra that renders, and the three that do not.
---

# Emphasis

`*italic*`, `**bold**` and `` `code` `` are CommonMark and need no extension. One extra is switched on beyond
them, and it is ~~strikethrough~~.

The other three are off, so they publish as the characters you typed. All four side by side, and note that
doubling the tilde is what separates the first row from the last:

| Written    | Published  |
| ---------- | ---------- |
| `~~text~~` | ~~text~~   |
| `==text==` | ==text==   |
| `^text^`   | ^text^     |
| `~text~`   | ~text~     |

That is worth knowing before you reach for a highlight, because nothing warns you: the page builds and the
markers simply show.

## Links

A bare host is linked for you, so www.example.com needs no brackets, and `https` is used for a `www` host
even where you wrote none.
