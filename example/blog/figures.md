---
title: Figures
date: 2026-10-08
description: Captioned blocks, and which end the caption goes on.
---

# Figures

A `^^^` fence wraps whatever you put inside it in a `<figure>`, and naming the fence adds a `<figcaption>`.
An image is the usual content, but anything goes:

^^^
| Directory | Published as |
| --------- | ------------ |
| `content` | the site     |
| `_site`   | the output   |
^^^ The two directories a build reads and writes

Name the opening fence instead and the caption lands above the content, because the caption is written where
you want it read:

^^^ A caption first, for once
Gelyn keeps the order you wrote.
^^^

A fence named at neither end is a plain `<figure>` with no caption at all, which is still useful for grouping
an image with the paragraph that belongs to it.
