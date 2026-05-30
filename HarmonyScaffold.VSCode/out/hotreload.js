"use strict";
var __createBinding = (this && this.__createBinding) || (Object.create ? (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    var desc = Object.getOwnPropertyDescriptor(m, k);
    if (!desc || ("get" in desc ? !m.__esModule : desc.writable || desc.configurable)) {
      desc = { enumerable: true, get: function() { return m[k]; } };
    }
    Object.defineProperty(o, k2, desc);
}) : (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    o[k2] = m[k];
}));
var __setModuleDefault = (this && this.__setModuleDefault) || (Object.create ? (function(o, v) {
    Object.defineProperty(o, "default", { enumerable: true, value: v });
}) : function(o, v) {
    o["default"] = v;
});
var __importStar = (this && this.__importStar) || (function () {
    var ownKeys = function(o) {
        ownKeys = Object.getOwnPropertyNames || function (o) {
            var ar = [];
            for (var k in o) if (Object.prototype.hasOwnProperty.call(o, k)) ar[ar.length] = k;
            return ar;
        };
        return ownKeys(o);
    };
    return function (mod) {
        if (mod && mod.__esModule) return mod;
        var result = {};
        if (mod != null) for (var k = ownKeys(mod), i = 0; i < k.length; i++) if (k[i] !== "default") __createBinding(result, mod, k[i]);
        __setModuleDefault(result, mod);
        return result;
    };
})();
Object.defineProperty(exports, "__esModule", { value: true });
exports.isActive = isActive;
exports.getPort = getPort;
exports.enableHotReload = enableHotReload;
exports.disableHotReload = disableHotReload;
const http = __importStar(require("http"));
const vscode = __importStar(require("vscode"));
let saveListener = null;
let hotReloadPort = 5567;
// Debounce: merge saves within 1 second
let debounceTimer = null;
let pendingFiles = new Map(); // filePath → code
const DEBOUNCE_MS = 1000;
function isActive() { return saveListener !== null; }
function getPort() { return hotReloadPort; }
function enableHotReload(outputChannel, statusItem) {
    if (saveListener) {
        return;
    }
    saveListener = vscode.workspace.onDidSaveTextDocument(async (doc) => {
        if (doc.languageId !== 'csharp') {
            return;
        }
        if (!doc.fileName.toLowerCase().includes('patch')) {
            return;
        }
        const code = doc.getText();
        if (!code.trim()) {
            return;
        }
        pendingFiles.set(doc.fileName, code);
        // Reset debounce timer
        if (debounceTimer) {
            clearTimeout(debounceTimer);
        }
        debounceTimer = setTimeout(() => {
            flushPending(outputChannel, statusItem);
        }, DEBOUNCE_MS);
    });
    outputChannel.appendLine('[HotReload] Enabled — save a .cs patch file to auto-inject');
}
function flushPending(outputChannel, statusItem) {
    if (pendingFiles.size === 0) {
        return;
    }
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
    const code = entries.map(([path, text]) => `// source: ${path}\n${text}`).join('\n\n');
    postHotReload(code).then(result => {
        if (result.success) {
            const tot = result.compileTimeMs + result.injectTimeMs;
            outputChannel.appendLine(`[HotReload] OK — compile ${result.compileTimeMs}ms, inject ${result.injectTimeMs}ms, total ${tot}ms`);
            statusItem.text = '$(check) HotReload';
            statusItem.tooltip = `Last hot reload OK — ${tot}ms total`;
            statusItem.backgroundColor = undefined;
            vscode.window.showInformationMessage(`$(check) Hot reload OK — ${tot}ms (compile ${result.compileTimeMs}ms + inject ${result.injectTimeMs}ms)`);
        }
        else {
            outputChannel.appendLine(`[HotReload] FAILED — ${result.error}`);
            outputChannel.show();
            statusItem.text = '$(error) HotReload';
            statusItem.tooltip = `Hot reload failed: ${result.error}`;
            statusItem.backgroundColor = new vscode.ThemeColor('statusBarItem.errorBackground');
            vscode.window.showErrorMessage(`Hot reload failed: ${result.error}`);
        }
    });
}
function disableHotReload() {
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
async function postHotReload(code) {
    return new Promise((resolve) => {
        const body = JSON.stringify({ code, assembly: 'Assembly-CSharp' });
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
                }
                catch {
                    resolve({ success: false, error: `Invalid response: ${data.substring(0, 200)}`, compileTimeMs: 0, injectTimeMs: 0 });
                }
            });
        });
        req.on('error', (err) => {
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
//# sourceMappingURL=hotreload.js.map