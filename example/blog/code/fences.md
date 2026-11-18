---
title: Fences
date: 2026-09-16
description: What a fenced block carries into the published HTML.
---

# Fences

A fence carries its info string as a `language-*` class. Nothing tokenizes the code at build time. Declare
`enableSyntaxHighlighting: true` in the front matter, and Shiki colors the block in the browser.

```python
def greet(name: str):
    print(f"Hello, {name}!")

if __name__ == "__main__":
    greet("Stefan")
```

A fence with no info string carries no class:

```
gelyn build --verbosity debug
```

Fences are also what keeps this page from rendering a table of its own. A `[TOD]` written inside one is
published as text, because only a paragraph holding the marker alone is replaced, and a fence never renders as
one:

```markdown
[TOD]
```
