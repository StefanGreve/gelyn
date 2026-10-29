---
title: Draft
description: A post that declares no date, and what that means for it.
---

# Draft

This page declares no `date`, which makes it a draft. No table of documents lists it, so nothing on this site
links here and you reached it by its URL or from the source tree.

Everything else is unchanged. Gelyn renders this page, writes it to the output directory, and serves it at
`/blog/draft.html` exactly as it does any other page, which is what makes the rule safe to rely on: a draft is
less discoverable, never unpublished.

Add a `date` to the front matter and the [blog index](index.html) lists it on the next build. Take the date
away again and it drops out.
