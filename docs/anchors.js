// Gives every heading that carries an id a link to itself, which copies its absolute URL when clicked.
// The markup and the class names here are the contract that anchors.css styles; neither half does
// anything on its own. The two glyphs follow Feather's `link` and `check` icons (MIT).

(() => {
    const main = document.querySelector("main");

    if (main === null) {
        return;
    }

    const icons = `
        <svg class="icon-link" viewBox="0 0 24 24" aria-hidden="true">
            <path d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71"/>
            <path d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71"/>
        </svg>
        <svg class="icon-check" viewBox="0 0 24 24" aria-hidden="true">
            <path d="M20 6L9 17L4 12"/>
        </svg>`;

    for (const heading of main.querySelectorAll(":is(h1, h2, h3, h4, h5, h6)[id]")) {
        const anchor = document.createElement("a");

        anchor.className = "heading-anchor";
        anchor.href = `#${heading.id}`;
        anchor.ariaLabel = anchor.title = `Copy link to ${heading.textContent.trim()}`;
        anchor.innerHTML = icons;

        heading.append(anchor);
    }

    let reset;

    main.addEventListener("click", async ({ target }) => {
        const anchor = target.closest("a.heading-anchor");

        if (anchor === null) {
            return;
        }

        try {
            // Undefined outside a secure context, so this throws there rather than claiming a copy that
            // never happened. The navigation runs either way, leaving an ordinary anchor link behind.
            await navigator.clipboard.writeText(anchor.href);
        } catch {
            return;
        }

        clearTimeout(reset);
        main.querySelector("[data-copied]")?.removeAttribute("data-copied");

        anchor.dataset.copied = "";
        reset = setTimeout(() => anchor.removeAttribute("data-copied"), 1200);
    });
})();
