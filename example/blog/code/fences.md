---
title: Fences
date: 2026-09-16
description: What a fenced block carries into the published HTML.
---

# Fences

A fence carries its info string as a `language-*` class. Nothing tokenizes the code, so style the block as a
whole or load a highlighter the same way you would any script.

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
published as text, because the brackets are escaped before the marker is looked for:

```markdown
[TOD]
```
