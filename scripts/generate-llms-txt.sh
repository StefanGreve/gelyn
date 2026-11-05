#!/usr/bin/env bash
#
# Writes llms.txt and llms-full.txt into the rendered documentation. llms.txt is the index that
# https://llmstxt.org specifies, and llms-full.txt carries the pages it lists as one Markdown file, so that an
# agent can read the documentation in a single request.
#
# Both files are derived from the pages and docs/gelyn.json on every run, so neither is checked in. Run this
# after gelyn build: the build reconciles its output directory and deletes any file it did not publish itself.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"

readonly CONTENT_DIR="${REPO_ROOT}/docs"
readonly CONFIG="${CONTENT_DIR}/gelyn.json"

# In reading order. dev.md addresses contributors, so the index lists it as optional and the bundle omits it.
readonly PAGES=(index usage configuration cli)

BASE_URL=""
REPOSITORY_URL=""
OUTPUT_DIR="${REPO_ROOT}/_docs"

usage() {
    cat <<EOF
Usage: $(basename "$0") --base-url=<url> --repository-url=<url> [--output=<directory>]

  -b, --base-url=<url>         URL the documentation is served from, such as https://stefangreve.github.io/gelyn.
  -r, --repository-url=<url>   URL of the source repository, such as https://github.com/StefanGreve/gelyn.
  -o, --output=<directory>     Rendered documentation to write into. Defaults to _docs in the repository root.
  -h, --help                   Show this message.

A value attaches with = or follows as the next argument: -b=https://example.com, --base-url https://example.com.
EOF
}

info() { printf '==> %s\n' "$1"; }
die() { printf 'error: %s\n' "$1" >&2; exit 1; }

while [[ $# -gt 0 ]]; do
    case "$1" in
        -b=*|--base-url=*) BASE_URL="${1#*=}"; shift ;;
        -r=*|--repository-url=*) REPOSITORY_URL="${1#*=}"; shift ;;
        -o=*|--output=*) OUTPUT_DIR="${1#*=}"; shift ;;
        -b|--base-url) [[ $# -ge 2 ]] || die "$1 requires a value."; BASE_URL="$2"; shift 2 ;;
        -r|--repository-url) [[ $# -ge 2 ]] || die "$1 requires a value."; REPOSITORY_URL="$2"; shift 2 ;;
        -o|--output) [[ $# -ge 2 ]] || die "$1 requires a value."; OUTPUT_DIR="$2"; shift 2 ;;
        -h|--help) usage; exit 0 ;;
        *) usage >&2; die "unknown argument '$1'." ;;
    esac
done

[[ -n "$BASE_URL" ]] || die "--base-url is required."
[[ -n "$REPOSITORY_URL" ]] || die "--repository-url is required."
[[ -d "$OUTPUT_DIR" ]] || die "${OUTPUT_DIR} does not exist. Run gelyn build first."

BASE_URL="${BASE_URL%/}"

command -v jq >/dev/null || die "jq is not installed."

config_value() {
    local value
    # -j leaves out the line ending, which jq on Windows writes as CRLF.
    value="$(jq -j --arg key "$1" '.[$key] // empty' "$CONFIG")"
    [[ -n "$value" ]] || die "${CONFIG} sets no $1."
    printf '%s' "$value"
}

TITLE="$(config_value Title)"
SUMMARY="$(config_value Description)"

header() {
    printf '# %s\n\n> %s\n' "$TITLE" "$SUMMARY"
}

page_body() {
    awk 'NR == 1 && $0 == "---" { fm = 1; next } fm && $0 == "---" { fm = 0; next } !fm' "${CONTENT_DIR}/$1.md"
}

# The first heading names the entry instead of the title, which for index.md is the name of the site. A note
# that would only repeat the summary above it is left out, since the proposal makes the note optional.
page_entry() {
    awk -v url="${BASE_URL}/$1.html" -v summary="$SUMMARY" '
        NR == 1 && $0 == "---" { fm = 1; next }
        fm && $0 == "---" { fm = 0; next }
        fm && sub(/^description: */, "") { description = $0; next }
        !fm && sub(/^# /, "") {
            printf "- [%s](%s)%s\n", $0, url, description == summary ? "" : ": " description
            exit
        }
    ' "${CONTENT_DIR}/$1.md"
}

# Each page opens with a blank line once its front matter is gone, so the header needs none of its own. A link
# between pages is relative, which a standalone file cannot resolve, so it is pointed at the rendered page.
{
    header
    for page in "${PAGES[@]}"; do
        page_body "$page"
        echo
    done
} | sed -E "s#\]\(([a-z]+)\.md(\#[^)]*)?\)#](${BASE_URL}/\1.html\2)#g" > "${OUTPUT_DIR}/llms-full.txt"

info "Wrote ${OUTPUT_DIR}/llms-full.txt."

{
    header
    echo
    echo "## Docs"
    echo
    echo "- [Full documentation](${BASE_URL}/llms-full.txt): The pages below as one Markdown file."
    for page in "${PAGES[@]}"; do
        page_entry "$page"
    done
    echo
    echo "## Optional"
    echo
    page_entry dev
    echo "- [Configuration schema](${BASE_URL}/schema.json): JSON Schema for gelyn.json."
    echo "- [Repository](${REPOSITORY_URL}): Source code, and an example site under example/."
} > "${OUTPUT_DIR}/llms.txt"

info "Wrote ${OUTPUT_DIR}/llms.txt."
