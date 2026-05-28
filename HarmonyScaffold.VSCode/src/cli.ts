import { execFile } from 'child_process';
import * as path from 'path';
import * as vscode from 'vscode';

function getCliPath(): string {
    return path.join(vscode.extensions.getExtension('harmony-scaffold.harmony-scaffold')
        ?.extensionPath ?? __dirname, 'bin', 'harmony-scaffold.exe');
}

export interface CliResult {
    success: boolean;
    data?: Record<string, unknown>;
    error?: string;
}

export function runCli(args: string[]): Promise<CliResult> {
    return new Promise((resolve) => {
        const cliPath = getCliPath();
        execFile(cliPath, args, { timeout: 30000 }, (err, stdout, stderr) => {
            if (err && !stdout) {
                resolve({ success: false, error: err.message });
                return;
            }
            try {
                resolve(JSON.parse(stdout || stderr));
            } catch {
                resolve({ success: false, error: stdout || stderr });
            }
        });
    });
}
