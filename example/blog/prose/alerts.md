---
title: Alerts
date: 2026-09-28
description: The five callouts, and the two ways of writing one that does not arrive.
---

# Alerts

> [!NOTE]
> `NOTE`, `TIP`, `IMPORTANT`, `WARNING` and `CAUTION` all render, icon included. The name is matched without
> regard to case, so `[!note]` is the same marker.

> [!TIP]
> Each one is an ordinary block quote until the marker is read, so nothing is lost on a renderer that does not
> know them.

> [!WARNING]
> The marker has to be the first line of the quote.

## A marker on the second line

A quote whose marker is not its first line keeps the marker as text:

> Not a marker.
> [!CAUTION]

## A name that is not one of the five

This one is worth more care, because the marker is read either way. The name is turned into a class without
being checked against the five, so the callout arrives with no icon and no title, and the marker line itself
is gone from the published page:

> [!INFO]
> `INFO`, `HINT`, `DANGER` and anything else you invent all behave like this.

The box is still there, and the themes still draw it, in their neutral tone instead of a color of its own. So
a misspelled `[!WARMING]` publishes a callout that says nothing about being a warning, and nothing warns you.
