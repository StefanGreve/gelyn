---
title: About
date: 2026-09-14
revised: 2026-10-26
description: Who I am and what I work on.
---

# About

Something about me, and a page to put the text extensions on.

~~Strikethrough~~ is the only emphasis extra enabled, so `==mark==`, `^super^` and `~sub~` stay literal.
A footnote[^1] links in both directions, and a bare host like www.example.com becomes an anchor.

Front matter
:   A YAML block at the top of the file. Its text starts at the fourth column, or this is a paragraph
    rather than a definition.

*[SSG]: Static Site Generator

Gelyn is an SSG, and every later mention of SSG carries the title.

A citation goes in doubled quotation marks, so ""premature optimization is the root of all evil"" becomes a
`cite` element rather than quoted text.

a. List extras add alpha ordered lists
b. and `i.`, `A.` or `I.` give the roman and uppercase variants, each setting `type` on the list

^^^
A figure block takes any content, not only an image.
^^^ The text after the closing fence becomes the caption.

[^1]: And lands here.
