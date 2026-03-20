// folderPicker.js — helpers for folder selection and file reading in Blazor WASM

window.folderPicker = {
    // Programmatically click a hidden <input type="file"> element by id
    open: function (inputId) {
        document.getElementById(inputId)?.click();
    },

    // Register a .NET instance reference so JS can notify Blazor when files are selected.
    // Avoids passing the full FileList through the Blazor event system (crashes debug proxy).
    init: function (inputId, dotNetRef) {
        const input = document.getElementById(inputId);
        if (!input) return;
        input.addEventListener('change', function () {
            dotNetRef.invokeMethodAsync('OnFolderSelectedCallback');
        });
    },

    // Return metadata for all files currently selected in the input.
    // Returns an array of { index, name, relativePath, size, lastModified }
    getFileList: function (inputId) {
        const input = document.getElementById(inputId);
        if (!input || !input.files) return [];
        const files = [];
        for (let i = 0; i < input.files.length; i++) {
            const f = input.files[i];
            files.push({
                index: i,
                name: f.name,
                relativePath: f.webkitRelativePath || f.name,
                size: f.size,
                lastModified: f.lastModified
            });
        }
        return files;
    },

    // Read a single file by numeric index and return its bytes as a base64 string
    readFileAsBase64: async function (inputId, indexOrName) {
        const input = document.getElementById(inputId);
        if (!input || !input.files) return null;

        let file = null;
        if (typeof indexOrName === 'number') {
            file = indexOrName < input.files.length ? input.files[indexOrName] : null;
        } else {
            // Search by file name
            for (let i = 0; i < input.files.length; i++) {
                if (input.files[i].name === indexOrName) {
                    file = input.files[i];
                    break;
                }
            }
        }
        if (!file) return null;

        return new Promise((resolve, reject) => {
            const reader = new FileReader();
            reader.onload = () => {
                // result is "data:<mime>;base64,<data>" — strip the prefix
                const b64 = reader.result.split(',')[1];
                resolve(b64);
            };
            reader.onerror = () => reject(reader.error);
            reader.readAsDataURL(file);
        });
    }
};
