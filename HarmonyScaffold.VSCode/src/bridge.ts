import * as http from 'http';
import { runCli } from './cli';

let server: http.Server | null = null;
let workspaceFolder: string | null = null;
const MAX_BODY = 1024 * 1024; // 1 MB

export interface BridgeStatus {
    running: boolean;
    port: number;
}

export function getStatus(): BridgeStatus {
    return { running: server !== null, port: 5566 };
}

export function startBridge(
    outputPath: string,
    onPatchGenerated: (filePath: string) => void,
    onError: (msg: string) => void,
    onLog: (msg: string) => void,
    onStatusChange: () => void
): void {
    if (server) { return; }
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
            } else {
                body += chunk;
            }
        });
        req.on('end', async () => {
            if (size > MAX_BODY) { return; }
            try {
                const data = JSON.parse(body);
                if (!data.class || !data.method) {
                    res.writeHead(400, { 'Content-Type': 'application/json' });
                    res.end(JSON.stringify({ success: false, error: 'Missing required fields: class and method' }));
                    return;
                }
                const args = buildCliArgs(data);
                const result = await runCli(args);

                if (result.success) {
                    const resultData = result.data as Record<string, unknown>;
                    const path = resultData.path as string;
                    onLog(`Patch generated: ${path}`);
                    onPatchGenerated(path);
                    res.writeHead(200, { 'Content-Type': 'application/json' });
                    res.end(JSON.stringify(result));
                } else {
                    onLog(`Error: ${result.error}`);
                    res.writeHead(400, { 'Content-Type': 'application/json' });
                    res.end(JSON.stringify(result));
                }
            } catch (e) {
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

    server.on('error', (err: NodeJS.ErrnoException) => {
        if (err.code === 'EADDRINUSE') {
            onError('Port 5566 is already in use. Is another bridge running?');
        } else {
            onError(`Bridge error: ${err.message}`);
        }
        try { server?.close(); } catch { /* ignore */ }
        server = null;
        onStatusChange();
    });
}

export function stopBridge(): void {
    if (server) {
        server.close();
        server = null;
    }
}

function buildCliArgs(data: Record<string, unknown>): string[] {
    const output = workspaceFolder || (data.output as string) || '.';
    const args = [
        'generate',
        '--class', (data.class as string) || '',
        '--method', (data.method as string) || '',
        '--type', (data.patchType as string) || '3',
        '--ret', (data.returnType as string) || 'System.Void',
        '--namespace', (data.namespace as string) || 'MyPatches',
        '--output', output
    ];

    if (data.author) { args.push('--author', data.author as string); }
    if (data.isStatic) { args.push('--static'); }
    if (data.useState) { args.push('--state'); }
    if (data.isInterface) { args.push('--interface'); }

    const params = data.params as Array<{ type: string; name: string }> | undefined;
    if (params && params.length > 0) {
        const paramStr = params.map(p => `${p.type} ${p.name}`).join(',');
        args.push('--params', paramStr);
    }

    const genericParams = data.genericParams as string[] | undefined;
    if (genericParams && genericParams.length > 0) {
        args.push('--generic', genericParams.join(','));
    }

    return args;
}
