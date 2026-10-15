---
title: Code
date: 2026-09-13
description: The first entry, and what this blog is going to be for.
enableDiagramRendering: true
---

# Code

A fence carries its info string as a `language-*` class. Nothing tokenizes the code, so style the block as a
whole or load a highlighter the same way you would any script.

```python
def greet(name: str):
    print(f"Hello, {name}!")

if __name__ == "__main__":
    greet("Stefan")
```

## Diagrams

A flowchart using [mermaid][mermaid]:

```mermaid
flowchart LR
    A[Markdown] --> B[gelyn build] --> C[_site]
```

The markup reaches the browser as literal text unless the page declares `enableDiagramRendering: true`.

## Alerts

> [!NOTE]
> `NOTE`, `TIP`, `IMPORTANT`, `WARNING` and `CAUTION` all render, icon included.

> [!WARNING]
> The marker has to be the first line of the quote.

[mermaid]: https://mermaid.js.org/syntax/flowchart.html
