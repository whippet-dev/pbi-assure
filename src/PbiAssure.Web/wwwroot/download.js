window.pbiAssureDownload = {
    open(mimeType, content) {
        const blob = new Blob([content], { type: mimeType });
        const objectUrl = URL.createObjectURL(blob);
        const reportWindow = window.open(objectUrl, "_blank");

        if (reportWindow === null) {
            URL.revokeObjectURL(objectUrl);
            return false;
        }

        reportWindow.opener = null;
        setTimeout(() => URL.revokeObjectURL(objectUrl), 60000);
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
