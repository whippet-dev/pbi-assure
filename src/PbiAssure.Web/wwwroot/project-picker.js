(() => {
    const files = new Map();
    const allowedExtensions = new Set([".pbip", ".pbir", ".json", ".tmdl", ".bim", ".pbism"]);
    const reportSuffix = ".report";
    const modelSuffix = ".semanticmodel";

    function pickerError(code, message) {
        return new Error(`[PBIASSURE:${code}] ${message}`);
    }

    function canonicalPath(path) {
        const parts = path.replaceAll("\\", "/").split("/").filter(Boolean);
        const result = [];
        for (const part of parts) {
            if (part === ".") continue;
            if (part === "..") {
                if (result.length === 0) throw pickerError("INVALID_PATH", "A selected path escapes the project folder.");
                result.pop();
                continue;
            }
            result.push(part);
        }
        if (result.length === 0) throw pickerError("INVALID_PATH", "A selected file has no project-relative path.");
        return result.join("/");
    }

    function extension(path) {
        const name = path.slice(path.lastIndexOf("/") + 1);
        const dot = name.lastIndexOf(".");
        return dot < 0 ? "" : name.slice(dot).toLowerCase();
    }

    function isRootProjectFile(path) {
        return !path.includes("/") && path.toLowerCase().endsWith(".pbip");
    }

    function isProjectDirectory(name) {
        const lower = name.toLowerCase();
        return lower.endsWith(reportSuffix) || lower.endsWith(modelSuffix);
    }

    function isRelevantProjectPath(path) {
        if (!allowedExtensions.has(extension(path))) return false;
        if (isRootProjectFile(path)) return true;
        const slash = path.indexOf("/");
        return slash > 0 && isProjectDirectory(path.slice(0, slash));
    }

    function createCollector(limits) {
        const selected = [];
        const paths = new Set();
        let visitedEntries = 0;
        let totalBytes = 0;
        let maximumDepth = 0;

        function visit(depth) {
            visitedEntries++;
            maximumDepth = Math.max(maximumDepth, depth);
            if (visitedEntries > limits.maxVisitedEntries) {
                throw pickerError("VISITED_LIMIT", `That folder contains more than ${limits.maxVisitedEntries.toLocaleString()} items. Choose the Power BI project folder itself.`);
            }
            if (depth > limits.maxDirectoryDepth) {
                throw pickerError("DEPTH_LIMIT", `That project is nested more than ${limits.maxDirectoryDepth.toLocaleString()} folders deep and cannot be opened safely.`);
            }
        }

        function add(path, file) {
            const canonical = canonicalPath(path);
            if (!isRelevantProjectPath(canonical)) return;
            const collisionKey = canonical.toLocaleLowerCase("en-US");
            if (paths.has(collisionKey)) {
                throw pickerError("DUPLICATE_PATH", `The selected project contains duplicate file paths that differ only by letter case: ${canonical}`);
            }
            if (file.size > limits.maxFileBytes) {
                throw pickerError("FILE_SIZE_LIMIT", `The metadata file ${file.name} is larger than ${Math.floor(limits.maxFileBytes / 1048576)} MiB.`);
            }
            if (selected.length >= limits.maxAcceptedFiles) {
                throw pickerError("FILE_COUNT_LIMIT", `That project contains more than ${limits.maxAcceptedFiles.toLocaleString()} metadata files and is too large for this browser version.`);
            }
            totalBytes += file.size;
            if (totalBytes > limits.maxTotalBytes) {
                throw pickerError("TOTAL_SIZE_LIMIT", `That project contains more than ${Math.floor(limits.maxTotalBytes / 1048576)} MiB of metadata and is too large for this browser version.`);
            }
            paths.add(collisionKey);
            selected.push({ path: canonical, file });
        }

        return {
            visit,
            add,
            result: () => ({ selected, visitedEntries, totalBytes, maximumDepth })
        };
    }

    function validateProjectRoots(paths) {
        const projects = paths.filter(isRootProjectFile);
        if (projects.length === 0) {
            throw pickerError("NO_PROJECT", "No Power BI project was found. Choose the folder that directly contains the .pbip file.");
        }
        if (projects.length > 1) {
            throw pickerError("MULTIPLE_PROJECTS", "More than one Power BI project was found. Choose one project folder at a time.");
        }
    }

    async function collectDirectory(directoryHandle, prefix, depth, collector) {
        for await (const [name, handle] of directoryHandle.entries()) {
            collector.visit(depth);
            const path = prefix ? `${prefix}/${name}` : name;
            if (handle.kind === "directory") {
                await collectDirectory(handle, path, depth + 1, collector);
            } else if (handle.kind === "file" && allowedExtensions.has(extension(path))) {
                collector.add(path, await handle.getFile());
            }
        }
    }

    async function chooseDirectoryHandle(limits) {
        const directoryHandle = await window.showDirectoryPicker({ mode: "read" });
        const rootEntries = [];
        const collector = createCollector(limits);
        for await (const [name, handle] of directoryHandle.entries()) {
            collector.visit(0);
            rootEntries.push([name, handle]);
        }

        validateProjectRoots(rootEntries
            .filter(([, handle]) => handle.kind === "file")
            .map(([name]) => name));

        for (const [name, handle] of rootEntries) {
            if (handle.kind === "file" && isRootProjectFile(name)) {
                collector.add(name, await handle.getFile());
            } else if (handle.kind === "directory" && isProjectDirectory(name)) {
                await collectDirectory(handle, name, 1, collector);
            }
        }

        return { displayName: directoryHandle.name, ...collector.result() };
    }

    function chooseFallback(limits) {
        return new Promise((resolve, reject) => {
            const input = document.getElementById("pbiassure-folder-picker");
            let settled = false;
            const cleanup = () => {
                input.removeEventListener("change", onChange);
                input.removeEventListener("cancel", onCancel);
            };
            const finish = callback => value => {
                if (settled) return;
                settled = true;
                cleanup();
                callback(value);
            };
            const complete = finish(resolve);
            const cancel = finish(reject);
            const onCancel = () => cancel(pickerError("CANCELLED", "Project selection was cancelled."));
            const onChange = () => {
                try {
                    const selectedFiles = [...input.files];
                    if (selectedFiles.length === 0) { onCancel(); return; }
                    if (selectedFiles.length > limits.maxVisitedEntries) {
                        throw pickerError("VISITED_LIMIT", `That folder contains more than ${limits.maxVisitedEntries.toLocaleString()} items. Choose the Power BI project folder itself.`);
                    }

                    const root = selectedFiles[0].webkitRelativePath.split("/")[0] || "Power BI project";
                    const relativeFiles = selectedFiles.map(file => ({
                        path: canonicalPath(file.webkitRelativePath.split("/").slice(1).join("/")),
                        file
                    }));
                    validateProjectRoots(relativeFiles.map(item => item.path));

                    const collector = createCollector(limits);
                    for (const item of relativeFiles) {
                        const depth = item.path.split("/").length - 1;
                        collector.visit(depth);
                        if (isRelevantProjectPath(item.path)) collector.add(item.path, item.file);
                    }
                    complete({ displayName: root, ...collector.result() });
                } catch (error) {
                    cancel(error);
                }
            };

            input.value = "";
            input.addEventListener("change", onChange);
            input.addEventListener("cancel", onCancel, { once: true });
            input.click();
        });
    }

    function setFiles(projectFiles) {
        files.clear();
        for (const item of projectFiles) files.set(item.path, item.file);
        return [...files].map(([relativePath, file]) => ({ relativePath, length: file.size }));
    }

    window.pbiAssureProjectPicker = {
        async choose(forceFallback, limits) {
            files.clear();
            try {
                let picked;
                if (!forceFallback && "showDirectoryPicker" in window) {
                    picked = await chooseDirectoryHandle(limits);
                } else {
                    picked = await chooseFallback(limits);
                }
                const manifest = setFiles(picked.selected);
                return {
                    displayName: picked.displayName,
                    files: manifest,
                    totalBytes: picked.totalBytes,
                    visitedEntries: picked.visitedEntries,
                    maximumDepth: picked.maximumDepth
                };
            } catch (error) {
                if (error && error.message && error.message.includes("[PBIASSURE:")) throw error;
                if (error && error.name === "AbortError") throw pickerError("CANCELLED", "Project selection was cancelled.");
                if (error && (error.name === "NotAllowedError" || error.name === "SecurityError")) {
                    throw pickerError("BLOCKED", "Folder access was blocked. Try the alternate folder picker.");
                }
                throw pickerError("PICKER_FAILED", "The project folder could not be opened. Try the alternate folder picker.");
            }
        },
        async read(relativePath) {
            const file = files.get(canonicalPath(relativePath));
            if (!file) throw pickerError("FILE_UNAVAILABLE", "A selected project file is no longer available.");
            return new Uint8Array(await file.arrayBuffer());
        }
    };
})();
