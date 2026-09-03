// Colour-scheme preference for the PBI Assure browser application.
//
// Loaded synchronously in the document head so a stored Light or Dark choice is on the root
// element before the first paint. Readers on System need nothing applied: the stylesheet already
// follows prefers-color-scheme.
//
// The only value this stores is that preference. Selected projects and analysis results are never
// written to browser storage.
(() => {
    const storageKey = "pbiassure-appearance";

    const read = () => {
        try {
            const stored = localStorage.getItem(storageKey);
            return stored === "light" || stored === "dark" ? stored : "system";
        } catch {
            return "system";
        }
    };

    const apply = choice => {
        if (choice === "light" || choice === "dark") {
            document.documentElement.dataset.theme = choice;
        } else {
            delete document.documentElement.dataset.theme;
        }
    };

    window.pbiAssureAppearance = {
        current: read,
        apply(choice) {
            apply(choice);
            try {
                if (choice === "system") {
                    localStorage.removeItem(storageKey);
                } else {
                    localStorage.setItem(storageKey, choice);
                }
            } catch {
                // Private browsing or a blocked-storage policy: the choice applies to this page only.
            }
        }
    };

    apply(read());
})();
