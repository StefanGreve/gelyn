---
title: Footnotes
date: 2026-10-02
description: References, citations, and abbreviations.
---

*[YAML]: YAML Ain't Markup Language

# Footnotes

A footnote reference lands where you write it and the note itself collects at the end of the page, linked both
ways.[^placement]

Numbering follows the order the references appear in, not the order the notes are written in, so moving a
paragraph renumbers its notes for you.[^order]

## Citations

A quoted phrase marked with doubled quotes becomes a ""citation"", which is a `<cite>` element and not a
block quote.

## Abbreviations

An abbreviation is declared once, anywhere on the page, and every later mention of YAML carries the title
without being marked up again.

[^placement]: The note can be declared anywhere, including directly under the paragraph that refers to it.
[^order]: Which is what makes a note safe to move with the text around it.
