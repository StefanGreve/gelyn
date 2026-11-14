# Security Policy

## Supported Versions

Gelyn is in its 0.x series, so only the latest release receives security fixes. A fix ships as a new release;
earlier releases are not patched.

## Reporting a Vulnerability

Report a vulnerability privately through [GitHub's private vulnerability reporting][report]. Please do not open
a public issue, a discussion, or a pull request for it.

Include what you can of the following:

- The output of `gelyn --info`
- The steps, the content, and the configuration that reproduce the problem
- What an attacker gains, such as reading or writing files outside the site

## Scope

In scope:

- Reading, writing, or deleting files outside the content and output directories during `gelyn build`
- `gelyn serve` answering requests with files outside the site, or listening on more than the loopback address
- The bundled themes and their scripts, which run on every site built with them
- The packages published on nuget.org

Out of scope:

- HTML in your own Markdown reaching the page, because raw HTML passes through by design
- Denial of service against `gelyn serve`, which is a local preview server, not a production server
- Vulnerabilities in a dependency, which belong to that project; tell me if Gelyn needs a release to pick up
  its fix

## What to Expect

This is a project maintained by one person in their spare time. I aim to acknowledge a report within seven
days and to keep you updated until it is resolved. Once a fix is released, the advisory is published with
credit to you, unless you prefer to stay anonymous.

<!-- References -->

[report]: https://github.com/StefanGreve/gelyn/security/advisories/new
