import { dotnet } from './_framework/dotnet.js';

try {
    const runtime = await dotnet.withDiagnosticTracing(false).create();
    await runtime.runMain(runtime.getConfig().mainAssemblyName, []);
    const exports = await runtime.getAssemblyExports(runtime.getConfig().mainAssemblyName);
    const composing = new WeakSet();
    const host = document.querySelector('#out');
    host.addEventListener('compositionstart', event => composing.add(event.target));
    host.addEventListener('compositionend', event => composing.delete(event.target));
    host.addEventListener('beforeinput', event => {
        if (event.inputType !== 'insertText' || event.isComposing || composing.has(event.target) ||
            event.defaultPrevented || event.data === null || !event.target.matches('.avalonia-input-element')) return;
        // Avalonia 12.1.3 forwards keyboard and composition events but omits plain insertText events.
        if (exports.Program.InsertText(event.data)) event.preventDefault();
    });
    document.querySelector('.loading')?.remove();
    host.dataset.ready = 'true';
} catch (error) {
    document.querySelector('#out').textContent = 'System 7 could not start. Reload this page to try again.';
    console.error(error);
}
