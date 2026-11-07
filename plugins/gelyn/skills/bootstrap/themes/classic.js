// Links the heading of a page below the content root back to the index above it: a page to the index of
// its own directory, an index to the index of the directory above.
(() => {
    const heading = document.querySelector("main > h1");
    const home = document.querySelector("header nav a");
    if (heading === null || home === null) return;

    const path = location.pathname;
    const directory = path.slice(0, path.lastIndexOf("/") + 1);
    const root = home.pathname.slice(0, home.pathname.lastIndexOf("/") + 1);
    if (directory === root) return;

    const name = path.slice(directory.length);
    const isIndex = name === "" || name === "index" || name === "index.html";
    const parent = directory.slice(0, directory.lastIndexOf("/", directory.length - 2) + 1);

    const link = document.createElement("a");
    link.className = "back";
    link.href = (isIndex ? parent : directory) + "index.html";
    link.setAttribute("aria-label", "Back to the parent page");
    heading.append(link);
})();
