// Triggers a browser download of base64-encoded content produced in .NET.
window.taxingDownload = function (fileName, base64, mimeType) {
    const link = document.createElement('a');
    link.download = fileName;
    link.href = `data:${mimeType};base64,${base64}`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};
