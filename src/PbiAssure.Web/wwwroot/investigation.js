// One delivery per new tab. Only the pending handshake holds a payload; no browser storage.
(() => {
    const pending = new Set();
    const timeoutMs = 20000;
    window.pbiAssureInvestigation = {
        open(snapshotJson, workspace = "overview") {
            if (!["overview", "unused", "check"].includes(workspace)) return Promise.resolve("failed");
            const url = new URL("investigation/index.html?v=__PBIASSURE_ASSET_VERSION__", document.baseURI);
            let viewer;
            try { viewer = window.open(url.href, "_blank"); }
            catch { return Promise.resolve("blocked"); }
            if (!viewer) return Promise.resolve("blocked");
            return new Promise(resolve => {
                const deliveryId = crypto.randomUUID();
                let snapshot, delivered = false, settled = false;
                const finish = status => {
                    if (settled) return;
                    settled = true;
                    window.removeEventListener("message", receive);
                    clearTimeout(timeout);
                    clearInterval(closed);
                    pending.delete(cancel);
                    snapshot = null;
                    snapshotJson = null;
                    resolve(status);
                };
                const receive = event => {
                    if (event.origin !== url.origin || event.source !== viewer) return;
                    const message = event.data;
                    if (message?.type === "pbi-assure-viewer-snapshot-ready" && !delivered) {
                        try {
                            snapshot = JSON.parse(snapshotJson);
                            delivered = true;
                            viewer.postMessage({type:"pbi-assure-viewer-snapshot", deliveryId, snapshot, workspace}, url.origin);
                            // postMessage clones the envelope; the receiver owns its independent snapshot.
                            snapshot = null;
                            snapshotJson = null;
                        } catch { finish("failed"); }
                    } else if (delivered && message?.type === "pbi-assure-viewer-snapshot-ack" &&
                        message.deliveryId === deliveryId && typeof message.accepted === "boolean") {
                        finish(message.accepted ? "opened" : "rejected");
                    }
                };
                const cancel = () => finish("cancelled");
                const timeout = setTimeout(() => finish("timeout"), timeoutMs);
                const closed = setInterval(() => { if (viewer.closed) finish("closed"); }, 200);
                pending.add(cancel);
                window.addEventListener("message", receive);
            });
        },
        cancelPending() { for (const cancel of pending) cancel(); }
    };
})();
