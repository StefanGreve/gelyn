---
title: Matrices
date: 2026-10-18
description: Environments that take more than one line.
enableMathRendering: true
---

# Matrices

A matrix needs an environment, and the environment goes inside the display delimiters:

$$
A = \begin{pmatrix}
1 & 2 \\
3 & 4
\end{pmatrix}
$$

So does an aligned derivation, where `&` marks the column every line lines up on:

$$
\begin{aligned}
(a + b)^2 &= (a + b)(a + b) \\
          &= a^2 + 2ab + b^2
\end{aligned}
$$

A blank line inside the block ends it, because the paragraph ends with it.
