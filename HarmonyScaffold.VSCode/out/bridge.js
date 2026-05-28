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
exports.getStatus = getStatus;
exports.startBridge = startBridge;
exports.stopBridge = stopBridge;
const http = __importStar(require("http"));
const cli_1 = require("./cli");
let server = null;
let workspaceFolder = null;
const MAX_BODY = 1024 * 1024; // 1 MB
function getStatus() {
    return { running: server !== null, port: 5566 };
}
function startBridge(outputPath, onPatchGenerated, onError, onLog, onStatusChange) {
    if (server) {
        return;
    }
    workspaceFolder = outputPath;
    server = http.createServer(async (req, res) => {
        res.setHeader('Access-Control-Allow-Origin', '*');
        res.setHeader('Access-Control-Allow-Methods', 'POST, OPTIONS');
        res.setHeader('Access-Control-Allow-Headers', 'Content-Type');
        if (req.method === 'OPTIONS') {
            res.writeHead(204);
            res.end();
            return;
        }
        if (req.method !== 'POST' || req.url !== '/') {
            res.writeHead(404);
            res.end(JSON.stringify({ success: false, error: 'Not found' }));
            return;
        }
        let body = '';
        let size = 0;
        req.on('data', chunk => {
            size += chunk.length;
            if (size > MAX_BODY) {
                res.writeHead(413, { 'Content-Type': 'application/json' });
                res.end(JSON.stringify({ success: false, error: 'Body too large' }));
                req.destroy();
            }
            else {
                body += chunk;
            }
        });
        req.on('end', async () => {
            if (size > MAX_BODY) {
                return;
            }
            try {
                const data = JSON.parse(body);
                if (!data.class || !data.method) {
                    res.writeHead(400, { 'Content-Type': 'application/json' });
                    res.end(JSON.stringify({ success: false, error: 'Missing required fields: class and method' }));
                    return;
                }
                const args = buildCliArgs(data);
                const result = await (0, cli_1.runCli)(args);
                if (result.success) {
                    const resultData = result.data;
                    const path = resultData.path;
                    onLog(`Patch generated: ${path}`);
                    onPatchGenerated(path);
                    res.writeHead(200, { 'Content-Type': 'application/json' });
                    res.end(JSON.stringify(result));
                }
                else {
                    onLog(`Error: ${result.error}`);
                    res.writeHead(400, { 'Content-Type': 'application/json' });
                    res.end(JSON.stringify(result));
                }
            }
            catch (e) {
                const errMsg = `Invalid request: ${e}`;
                onError(errMsg);
                res.writeHead(400, { 'Content-Type': 'application/json' });
                res.end(JSON.stringify({ success: false, error: errMsg }));
            }
        });
    });
    server.listen(5566, '127.0.0.1', () => {
        onLog('Bridge started on http://127.0.0.1:5566');
    });
    server.on('error', (err) => {
        if (err.code === 'EADDRINUSE') {
            onError('Port 5566 is already in use. Is another bridge running?');
        }
        else {
            onError(`Bridge error: ${err.message}`);
        }
        try {
            server?.close();
        }
        catch { /* ignore */ }
        server = null;
        onStatusChange();
    });
}
function stopBridge() {
    if (server) {
        server.close();
        server = null;
    }
}
function buildCliArgs(data) {
    const output = workspaceFolder || data.output || '.';
    const args = [
        'generate',
        '--class', data.class || '',
        '--method', data.method || '',
        '--type', data.patchType || '3',
        '--ret', data.returnType || 'System.Void',
        '--namespace', data.namespace || 'MyPatches',
        '--output', output
    ];
    if (data.author) {
        args.push('--author', data.author);
    }
    if (data.isStatic) {
        args.push('--static');
    }
    if (data.useState) {
        args.push('--state');
    }
    if (data.isInterface) {
        args.push('--interface');
    }
    const params = data.params;
    if (params && params.length > 0) {
        const paramStr = params.map(p => `${p.type} ${p.name}`).join(',');
        args.push('--params', paramStr);
    }
    const genericParams = data.genericParams;
    if (genericParams && genericParams.length > 0) {
        args.push('--generic', genericParams.join(','));
    }
    return args;
}
//# sourceMappingURL=bridge.js.map