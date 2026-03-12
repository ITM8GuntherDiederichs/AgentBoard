// AgentBoard — global JS helpers

/**
 * Triggers a browser file-download from a base64-encoded byte array.
 * Called from Blazor via IJSRuntime when downloading generated ZIP packages.
 *
 * @param {string} base64    - Base64-encoded file content
 * @param {string} fileName  - Suggested download file name
 * @param {string} mimeType  - MIME type (e.g. "application/zip")
 */
window.downloadFileFromBase64 = function (base64, fileName, mimeType) {
    const link = document.createElement('a');
    link.href = 'data:' + mimeType + ';base64,' + base64;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};
