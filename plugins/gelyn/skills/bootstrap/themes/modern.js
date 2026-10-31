// Builds the mobile banner that modern.css styles below 40rem: a menu button, and a sheet carrying copies of
// every navigation entry but the first. The class names here are the contract that stylesheet styles, so
// neither file does anything on its own. The two glyphs follow Feather's `menu` and `x` icons (MIT).

(() => {
    const header = document.querySelector("header");
    const list = header?.querySelector("nav > ul");

    if (list == null || list.children.length < 2) {
        return;
    }

    const panel = document.createElement("div");
    const items = document.createElement("ul");

    panel.className = "nav-panel";
    panel.id = "nav-panel";

    // The same list serves as the banner above the breakpoint, so these are copies and the originals stay in
    // place. Whichever set does not apply is display: none, which also takes it out of the accessibility tree.
    for (const item of [...list.children].slice(1)) {
        items.append(item.cloneNode(true));
    }

    panel.append(items);

    const toggle = document.createElement("button");

    toggle.className = "nav-toggle";
    toggle.type = "button";
    toggle.setAttribute("aria-controls", "nav-panel");
    toggle.innerHTML = `
        <svg class="icon-menu" viewBox="0 0 24 24" aria-hidden="true">
            <path d="M4 7h16M4 12h16M4 17h16"/>
        </svg>
        <svg class="icon-close" viewBox="0 0 24 24" aria-hidden="true">
            <path d="M6 6l12 12M18 6L6 18"/>
        </svg>`;

    const setOpen = (open) => {
        header.toggleAttribute("data-open", open);
        toggle.ariaExpanded = String(open);
        toggle.ariaLabel = open ? "Close menu" : "Menu";
    };

    setOpen(false);

    toggle.addEventListener("click", () => setOpen(!header.hasAttribute("data-open")));

    document.addEventListener("keydown", ({ key }) => {
        if (key === "Escape") {
            setOpen(false);
        }
    });

    header.append(toggle, panel);
})();

// Gives a diagram the appearance the rest of the sheet already follows. Mermaid resolves none of its own, and
// gelyn puts the instance on window because its import otherwise stays inside the module in the head.

(() => {
    const mermaid = window.mermaid;

    if (mermaid == null) {
        return;
    }

    const dark = matchMedia("(prefers-color-scheme: dark)");
    const apply = () => mermaid.initialize({ startOnLoad: true, theme: dark.matches ? "dark" : "default" });
    const sources = new Map();

    // A render replaces each pre with the SVG and discards the diagram source, so drawing one again after the
    // preference changes needs a copy taken before the first render, which the load event defers until later.
    for (const diagram of document.querySelectorAll("pre.mermaid")) {
        sources.set(diagram, diagram.textContent);
    }

    apply();

    dark.addEventListener("change", async () => {
        for (const [diagram, source] of sources) {
            diagram.textContent = source;
            diagram.removeAttribute("data-processed");
        }

        apply();
        await mermaid.run();
    });
})();
