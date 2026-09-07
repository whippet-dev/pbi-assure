// The viewer is same-origin, already cached, and re-announces readiness every 100ms, so a working
// handshake settles in well under a second. Ten seconds leaves room for a throttled background tab
// and a large report payload while still failing fast enough that nobody waits on a dead viewer.
const PBIASSURE_VIEWER_ACK_TIMEOUT_MS = 10000;

window.pbiAssureDownload = {
    // Resolves only once the viewer has confirmed it accepted the report: opening a tab is not the
    // same as delivering to it. Returns one of "opened", "blocked", "timeout", "rejected", "failed".
    open(mimeType, content) {
        const viewerUrl = new URL(
            "report-viewer.html?v=__PBIASSURE_ASSET_VERSION__",
            document.baseURI);
        const reportWindow = window.open(viewerUrl.href, "_blank");

        if (reportWindow === null) {
            return Promise.resolve("blocked");
        }

        // Ties the acknowledgement to this delivery, so a late reply to an earlier open cannot
        // report success for this one.
        const deliveryId = crypto.randomUUID();

        return new Promise(resolve => {
            let settled = false;
            let delivered = false;

            const finish = status => {
                if (settled) {
                    return;
                }
                settled = true;
                window.removeEventListener("message", handleViewerMessage);
                clearTimeout(timeoutHandle);
                resolve(status);
            };

            const handleViewerMessage = event => {
                if (event.origin !== viewerUrl.origin || event.source !== reportWindow) {
                    return;
                }

                const message = event.data;
                if (message?.type === "pbi-assure-report-viewer-ready") {
                    // The viewer repeats this until it has the report; deliver the payload once.
                    if (delivered) {
                        return;
                    }
                    delivered = true;
                    try {
                        reportWindow.postMessage({
                            type: "pbi-assure-report-content",
                            deliveryId,
                            mimeType,
                            content
                        }, viewerUrl.origin);
                    } catch {
                        finish("failed");
                    }
                    return;
                }

                if (message?.type === "pbi-assure-report-viewer-ack" &&
                    message.deliveryId === deliveryId) {
                    finish(message.accepted === true ? "opened" : "rejected");
                }
            };

            const timeoutHandle = setTimeout(() => finish("timeout"), PBIASSURE_VIEWER_ACK_TIMEOUT_MS);
            window.addEventListener("message", handleViewerMessage);
        });
    },

    save(filename, mimeType, content) {
        const blob = new Blob([content], { type: mimeType });
        const objectUrl = URL.createObjectURL(blob);
        const link = document.createElement("a");
        link.href = objectUrl;
        link.download = filename;
        link.style.display = "none";
        document.body.append(link);
        link.click();
        link.remove();
        setTimeout(() => URL.revokeObjectURL(objectUrl), 0);
    }
};
