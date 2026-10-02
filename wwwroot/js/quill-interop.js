// wwwroot/js/quill-interop.js
// ES Module providing dynamic, on-demand loading of Quill assets and editor lifecycle management.

let quillLoadPromise = null;

function ensureQuillLoaded() {
    if (window.Quill) {
        return Promise.resolve();
    }

    if (quillLoadPromise) {
        return quillLoadPromise;
    }

    quillLoadPromise = new Promise((resolve, reject) => {
        // 1. Inject Quill Snow CSS on-demand into <head> if not already present
        if (!document.getElementById('quill-snow-css')) {
            const link = document.createElement('link');
            link.id = 'quill-snow-css';
            link.rel = 'stylesheet';
            link.href = '/lib/quill/quill.snow.css';
            link.onerror = () => {
                // Fallback to cdnjs if local stylesheet fails
                link.href = 'https://cdnjs.cloudflare.com/ajax/libs/quill/2.0.2/quill.snow.css';
            };
            document.head.appendChild(link);
        }

        // 2. Inject Quill core JS on-demand from local hosting /lib/quill/quill.js
        const existingScript = document.getElementById('quill-core-js');
        if (existingScript) {
            existingScript.addEventListener('load', () => resolve());
            existingScript.addEventListener('error', (err) => reject(err));
            return;
        }

        const script = document.createElement('script');
        script.id = 'quill-core-js';
        script.src = '/lib/quill/quill.js';
        script.async = true;
        script.onload = () => resolve();
        script.onerror = () => {
            console.warn('Local /lib/quill/quill.js not found or blocked. Attempting CDN fallback...');
            const fallbackScript = document.createElement('script');
            fallbackScript.id = 'quill-core-fallback-js';
            fallbackScript.src = 'https://cdnjs.cloudflare.com/ajax/libs/quill/2.0.2/quill.js';
            fallbackScript.async = true;
            fallbackScript.onload = () => resolve();
            fallbackScript.onerror = () => reject(new Error('Failed to load Quill library. Please run .\\Download-Quill.ps1 in the project root to host assets locally.'));
            document.body.appendChild(fallbackScript);
        };
        document.body.appendChild(script);
    });

    return quillLoadPromise;
}

const instances = {};

export async function init(editorId, dotNetHelper, initialContent, maxLength, readOnly) {
    const container = document.getElementById(editorId);
    if (!container) return;

    // Await on-demand loading of Quill before initializing instance
    await ensureQuillLoaded();

    if (!window.Quill) {
        console.error('Quill library could not be initialized.');
        return;
    }

    // Cleanup existing instance if any
    if (instances[editorId]) {
        instances[editorId] = null;
    }

    const toolbarOptions = [
        [{ 'header': [1, 2, 3, false] }],
        ['bold', 'italic', 'underline', 'strike'],
        [{ 'color': [] }, { 'background': [] }],
        [{ 'list': 'ordered' }, { 'list': 'bullet' }],
        ['blockquote', 'code-block'],
        ['link', 'image'],
        ['clean']
    ];

    const quill = new window.Quill(container, {
        theme: 'snow',
        readOnly: readOnly || false,
        placeholder: readOnly ? '' : 'Write article description, steps, or insert images...',
        modules: {
            toolbar: readOnly ? false : toolbarOptions
        }
    });

    if (initialContent) {
        quill.root.innerHTML = initialContent;
    }

    instances[editorId] = quill;

    if (!readOnly) {
        quill.on('text-change', () => {
            const html = quill.root.innerHTML;
            const text = quill.getText();
            let textLength = text.length - 1; // subtract trailing newline

            if (maxLength && textLength > maxLength) {
                quill.deleteText(maxLength, textLength);
                textLength = maxLength;
            }

            dotNetHelper.invokeMethodAsync('OnContentChanged', html, textLength);
        });
    }
}

export function setContent(editorId, html) {
    const quill = instances[editorId];
    if (quill) {
        quill.root.innerHTML = html || '';
    }
}

export function getContent(editorId) {
    const quill = instances[editorId];
    return quill ? quill.root.innerHTML : '';
}

export function destroy(editorId) {
    if (instances[editorId]) {
        delete instances[editorId];
    }
}

// Global fallback object for backwards-compatibility
window.fluentQuill = {
    instances,
    init,
    setContent,
    getContent,
    destroy
};
