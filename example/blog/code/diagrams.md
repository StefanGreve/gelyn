---
title: Diagrams
date: 2026-09-24
description: A fence whose contents are drawn instead of printed.
enableDiagramRendering: true
---

# Diagrams

A fence tagged `mermaid` is handed to [mermaid][mermaid] in the browser instead of being printed as code:

```mermaid
flowchart LR
    A[Markdown] --> B[gelyn build] --> C[_site]
```

The markup reaches the browser as literal text unless the page declares `enableDiagramRendering: true`. This
page does; the index above it does not, because the flag applies to the page that declares it and is inherited
by nothing.

```mermaid
stateDiagram-v2
    [*] --> Draft
    Draft --> Published: date added
    Published --> Draft: date removed
```

[mermaid]: https://mermaid.js.org/syntax/flowchart.html
