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
        // Origin and source are checked before anything is answered, so a message from anywhere else
        // is ignored entirely rather than told whether it was understood.
        if (event.origin !== window.location.origin ||
            event.source !== sourceWindow ||
            event.data?.type !== "pbi-assure-report-content") {
            return;
        }

        const acknowledge = accepted => sourceWindow.postMessage(
            { type: "pbi-assure-report-viewer-ack", deliveryId: event.data.deliveryId, accepted },
            window.location.origin);

        if (typeof event.data.content !== "string" ||
            !event.data.mimeType?.startsWith("text/html")) {
            acknowledge(false);
            return;
        }

        clearInterval(readyInterval);
        window.removeEventListener("message", receiveReport);
        // Acknowledged once the payload has been validated and this viewer has committed to
        // rendering it. document.write replaces this document, so nothing can be sent afterwards —
        // and the opener reference the acknowledgement needs is cleared immediately below.
        acknowledge(true);
        window.opener = null;
        document.open();
        document.write(event.data.content);
        document.close();
    };

    window.addEventListener("message", receiveReport);
    const readyInterval = setInterval(announceReady, 100);
    announceReady();
})();
