// The analysis stays in memory. Only this newly opened, same-origin tab receives it.
window.pbiAssureInvestigation = {
    open(content, projectName, workspace) {
        const url = new URL("investigation/index.html", document.baseURI);
        const viewer = window.open(url.href, "_blank");
        if (!viewer) return Promise.resolve("blocked");
        const deliveryId = crypto.randomUUID();
        return new Promise(resolve => {
            let delivered = false;
            const finish = status => {
                clearTimeout(timeout);
                window.removeEventListener("message", receive);
                resolve(status);
            };
            const receive = event => {
                if (event.origin !== url.origin || event.source !== viewer) return;
                if (event.data?.type === "pbi-assure-investigation-ready" && !delivered) {
                    delivered = true;
                    try {
                        viewer.postMessage({type: "pbi-assure-investigation", deliveryId,
                            content, projectName, workspace}, url.origin);
                    } catch { finish("failed"); }
                }
                if (event.data?.type === "pbi-assure-investigation-ack" && event.data.deliveryId === deliveryId)
                    finish(event.data.accepted ? "opened" : "rejected");
            };
            const timeout = setTimeout(() => finish("timeout"), 15000);
            window.addEventListener("message", receive);
        });
    }
};
