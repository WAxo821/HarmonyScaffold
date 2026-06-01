import * as http from 'http';
import * as vscode from 'vscode';

let saveListener: vscode.Disposable | null = null;
let hotReloadPort = 5567;

// Debounce: merge saves within 1 second
let debounceTimer: ReturnType<typeof setTimeout> | null = null;
let pendingFiles = new Map<string, string>(); // filePath → code
const DEBOUNCE_MS = 1000;

export function isActive(): boolean { return saveListener !== null; }
export function getPort(): number { return hotReloadPort; }

export function enableHotReload(
    outputChannel: vscode.OutputChannel,
    statusItem: vscode.StatusBarItem
): void {
    if (saveListener) { return; }

    saveListener = vscode.workspace.onDidSaveTextDocument(async (doc) => {
        if (doc.languageId !== 'csharp') { return; }
        if (!doc.fileName.toLowerCase().includes('patch')) { return; }

        const code = doc.getText();
        if (!code.trim()) { return; }

        pendingFiles.set(doc.fileName, code);

        // Reset debounce timer
        if (debounceTimer) { clearTimeout(debounceTimer); }
        debounceTimer = setTimeout(() => {
            flushPending(outputChannel, statusItem);
        }, DEBOUNCE_MS);
    });

    outputChannel.appendLine('[HotReload] Enabled — save a .cs patch file to auto-inject');
}

function flushPending(
    outputChannel: vscode.OutputChannel,
    statusItem: vscode.StatusBarItem
): void {
    if (pendingFiles.size === 0) { return; }

    const entries = Array.from(pendingFiles.entries());
    pendingFiles.clear();

    const total = entries.length;
    outputChannel.appendLine(`[HotReload] Sending ${total} file(s)...`);
    outputChannel.show();

    statusItem.text = '$(sync~spin) HotReload';
    statusItem.tooltip = `Compiling ${total} file(s)...`;
    statusItem.backgroundColor = undefined;

    // Send the last file (most recently saved) — covers the common single-file case.
    // For multi-file, merge all code together.
    const code = entries.map(([path, text]) =>
        `// source: ${path}\n${text}`
    ).join('\n\n');

    const outputDir = vscode.workspace.getConfiguration('harmony-scaffold').get<string>('hotReloadOutput')
        || vscode.workspace.workspaceFolders?.[0]?.uri.fsPath
        || '';

    postHotReload(code, outputDir).then(result => {
        if (result.success) {
            const tot = result.compileTimeMs + result.injectTimeMs;
            outputChannel.appendLine(
                `[HotReload] OK — compile ${result.compileTimeMs}ms, inject ${result.injectTimeMs}ms, total ${tot}ms`
            );
            statusItem.text = '$(check) HotReload';
            statusItem.tooltip = `Last hot reload OK — ${tot}ms total`;
            statusItem.backgroundColor = undefined;
            vscode.window.showInformationMessage(
                `$(check) Hot reload OK — ${tot}ms (compile ${result.compileTimeMs}ms + inject ${result.injectTimeMs}ms)`
            );
        } else {
            outputChannel.appendLine(`[HotReload] FAILED — ${result.error}`);
            outputChannel.show();
            statusItem.text = '$(error) HotReload';
            statusItem.tooltip = `Hot reload failed: ${result.error}`;
            statusItem.backgroundColor = new vscode.ThemeColor('statusBarItem.errorBackground');
            vscode.window.showErrorMessage(`Hot reload failed: ${result.error}`);
        }
    });
}

export function disableHotReload(): void {
    if (saveListener) {
        saveListener.dispose();
        saveListener = null;
    }
    if (debounceTimer) {
        clearTimeout(debounceTimer);
        debounceTimer = null;
    }
    pendingFiles.clear();
}

async function postHotReload(code: string, outputDir: string): Promise<HotReloadResponse> {
    return new Promise((resolve) => {
        const body = JSON.stringify({ code, assembly: 'Assembly-CSharp', output: outputDir });
        const req = http.request({
            hostname: '127.0.0.1',
            port: hotReloadPort,
            path: '/hotreload',
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Content-Length': Buffer.byteLength(body)
            },
            timeout: 15000
        }, (res) => {
            let data = '';
            res.on('data', chunk => data += chunk);
            res.on('end', () => {
                try {
                    resolve(JSON.parse(data));
                } catch {
                    resolve({ success: false, error: `Invalid response: ${data.substring(0, 200)}`, compileTimeMs: 0, injectTimeMs: 0 });
                }
            });
        });

        req.on('error', (err: NodeJS.ErrnoException) => {
            resolve({ success: false, error: `Cannot reach server (port ${hotReloadPort}): ${err.message}`, compileTimeMs: 0, injectTimeMs: 0 });
        });

        req.on('timeout', () => {
            req.destroy();
            resolve({ success: false, error: 'Hot reload timed out', compileTimeMs: 0, injectTimeMs: 0 });
        });

        req.write(body);
        req.end();
    });
}

interface HotReloadResponse {
    success: boolean;
    error?: string;
    message?: string;
    compileTimeMs: number;
    injectTimeMs: number;
}
