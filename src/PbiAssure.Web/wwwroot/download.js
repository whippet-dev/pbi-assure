window.pbiAssureDownload = {
    open(mimeType, content) {
        const viewerUrl = new URL(
            "report-viewer.html?v=__PBIASSURE_ASSET_VERSION__",
            document.baseURI);
        const reportWindow = window.open(viewerUrl.href, "_blank");

        if (reportWindow === null) {
            return false;
        }

        const handleViewerReady = event => {
            if (event.origin !== viewerUrl.origin ||
                event.source !== reportWindow ||
                event.data?.type !== "pbi-assure-report-viewer-ready") {
                return;
            }

            window.removeEventListener("message", handleViewerReady);
            reportWindow.postMessage({
                type: "pbi-assure-report-content",
                mimeType,
                content
            }, viewerUrl.origin);
        };

        window.addEventListener("message", handleViewerReady);
        setTimeout(() => window.removeEventListener("message", handleViewerReady), 30000);
        return true;
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
