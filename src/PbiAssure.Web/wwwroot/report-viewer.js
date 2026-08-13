(() => {
    const sourceWindow = window.opener;
    const status = document.getElementById("viewer-status");
    let attempts = 0;

    if (sourceWindow === null) {
        status.textContent = "Open a report from the PBI Assure browser application.";
        return;
    }

    const announceReady = () => {
        attempts++;
        sourceWindow.postMessage(
            { type: "pbi-assure-report-viewer-ready" },
            window.location.origin);
        if (attempts >= 100) {
            clearInterval(readyInterval);
            status.textContent = "The report could not be received. Return to PBI Assure and try again.";
        }
    };

    const receiveReport = event => {
        if (event.origin !== window.location.origin ||
            event.source !== sourceWindow ||
            event.data?.type !== "pbi-assure-report-content" ||
            typeof event.data.content !== "string" ||
            !event.data.mimeType?.startsWith("text/html")) {
            return;
        }

        clearInterval(readyInterval);
        window.removeEventListener("message", receiveReport);
        window.opener = null;
        document.open();
        document.write(event.data.content);
        document.close();
    };

    window.addEventListener("message", receiveReport);
    const readyInterval = setInterval(announceReady, 100);
    announceReady();
})();
