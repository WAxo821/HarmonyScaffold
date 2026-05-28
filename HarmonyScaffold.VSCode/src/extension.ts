import * as vscode from 'vscode';
import { runCli } from './cli';
import { startBridge, stopBridge, getStatus } from './bridge';

let statusBarItem: vscode.StatusBarItem;

export function activate(context: vscode.ExtensionContext) {
    const outputChannel = vscode.window.createOutputChannel('Harmony Scaffold');
    context.subscriptions.push(outputChannel);

    // Status bar
    statusBarItem = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Right, 100);
    statusBarItem.command = 'harmony-scaffold.toggleBridge';
    context.subscriptions.push(statusBarItem);
    updateStatusBar();

    // ---- init ----
    context.subscriptions.push(
        vscode.commands.registerCommand('harmony-scaffold.init', async (uri?: vscode.Uri) => {
            const name = await vscode.window.showInputBox({
                prompt: 'Plugin name',
                placeHolder: 'MyPlugin',
                validateInput: (v) => v ? null : 'Name is required'
            });
            if (!name) { return; }

            const folder = uri?.fsPath ?? vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;
            if (!folder) {
                vscode.window.showErrorMessage('No folder selected. Right-click a folder in Explorer or open a workspace.');
                return;
            }

            const result = await runCli(['init', '--name', name, '--output', folder]);
            if (result.success) {
                const data = result.data as Record<string, unknown>;
                outputChannel.appendLine(`Project created: ${data.path}`);
                outputChannel.show();
                vscode.window.showInformationMessage(`BepInEx plugin "${name}" initialized!`);
            } else {
                vscode.window.showErrorMessage(result.error ?? 'Unknown error');
            }
        })
    );

    // ---- generate ----
    context.subscriptions.push(
        vscode.commands.registerCommand('harmony-scaffold.generate', async () => {
            const className = await vscode.window.showInputBox({
                prompt: 'Target class name',
                placeHolder: 'Player',
                validateInput: (v) => v ? null : 'Class name is required'
            });
            if (!className) { return; }

            const methodName = await vscode.window.showInputBox({
                prompt: 'Target method name',
                placeHolder: 'TakeDamage',
                validateInput: (v) => v ? null : 'Method name is required'
            });
            if (!methodName) { return; }

            const params = await vscode.window.showInputBox({
                prompt: 'Parameters (optional, e.g. "int amount, float speed")',
                placeHolder: 'int amount, float speed'
            });

            const patchType = await vscode.window.showQuickPick([
                { label: 'Prefix', description: 'Runs before the original method' },
                { label: 'Postfix', description: 'Runs after the original method' },
                { label: 'Prefix + Postfix', description: 'Both before and after' },
                { label: 'Transpiler', description: 'Modifies IL code directly' },
                { label: 'Finalizer', description: 'Runs in finally block (exception-safe)' }
            ], { placeHolder: 'Select patch type' });
            if (!patchType) { return; }

            const patchTypeMap: Record<string, string> = {
                'Prefix': '1', 'Postfix': '2', 'Prefix + Postfix': '3',
                'Transpiler': '4', 'Finalizer': '5'
            };

            const returnType = await vscode.window.showInputBox({
                prompt: 'Return type (leave empty for void)',
                placeHolder: 'System.Void'
            });

            const ns = await vscode.window.showInputBox({
                prompt: 'Namespace (optional)',
                placeHolder: 'MyMod.Patches'
            });

            const author = await vscode.window.showInputBox({
                prompt: 'Author (optional)',
                placeHolder: 'YourName'
            });

            const isStatic = await vscode.window.showQuickPick(['No', 'Yes'], {
                placeHolder: 'Is the target method static?'
            });

            const useState = await vscode.window.showQuickPick(['No', 'Yes'], {
                placeHolder: 'Need __state for Prefix→Postfix communication?'
            });

            const workspaceFolder = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;

            const args = [
                'generate',
                '--class', className,
                '--method', methodName,
                '--type', patchTypeMap[patchType.label],
                '--ret', returnType || 'System.Void',
                '--namespace', ns || 'MyMod.Patches'
            ];

            if (author) { args.push('--author', author); }
            if (workspaceFolder) { args.push('--output', workspaceFolder); }

            if (params) { args.push('--params', params); }
            if (isStatic === 'Yes') { args.push('--static'); }
            if (useState === 'Yes') { args.push('--state'); }

            const result = await runCli(args);
            if (result.success) {
                const data = result.data as Record<string, unknown>;
                if (data.path) {
                    outputChannel.appendLine(`Patch generated: ${data.path}`);
                    outputChannel.show();
                    const doc = await vscode.workspace.openTextDocument(data.path as string);
                    await vscode.window.showTextDocument(doc);
                } else {
                    outputChannel.appendLine('Patch generated (no output path specified).');
                    outputChannel.show();
                }
                vscode.window.showInformationMessage('Harmony patch generated!');
            } else {
                vscode.window.showErrorMessage(result.error ?? 'Generation failed');
            }
        })
    );

    // ---- bridge ----
    context.subscriptions.push(
        vscode.commands.registerCommand('harmony-scaffold.startBridge', () => {
            const workspaceFolder = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath || '.';
            startBridge(
                workspaceFolder,
                async (filePath) => {
                    const doc = await vscode.workspace.openTextDocument(filePath);
                    await vscode.window.showTextDocument(doc);
                },
                (msg) => { outputChannel.appendLine(`[Bridge ERROR] ${msg}`); outputChannel.show(); },
                (msg) => { outputChannel.appendLine(`[Bridge] ${msg}`); },
                () => { updateStatusBar(); }
            );
            updateStatusBar();
            vscode.window.showInformationMessage('Harmony bridge started on port 5566');
        })
    );

    context.subscriptions.push(
        vscode.commands.registerCommand('harmony-scaffold.stopBridge', () => {
            stopBridge();
            updateStatusBar();
            vscode.window.showInformationMessage('Harmony bridge stopped');
        })
    );

    context.subscriptions.push(
        vscode.commands.registerCommand('harmony-scaffold.toggleBridge', () => {
            const status = getStatus();
            if (status.running) {
                vscode.commands.executeCommand('harmony-scaffold.stopBridge');
            } else {
                vscode.commands.executeCommand('harmony-scaffold.startBridge');
            }
        })
    );

    // Auto-start bridge if desired (off by default)
    // vscode.commands.executeCommand('harmony-scaffold.startBridge');
}

function updateStatusBar() {
    const status = getStatus();
    if (status.running) {
        statusBarItem.text = '$(radio-tower) Harmony Bridge';
        statusBarItem.tooltip = 'Harmony bridge running on :5566 — click to stop';
        statusBarItem.backgroundColor = undefined;
    } else {
        statusBarItem.text = '$(circle-slash) Harmony Bridge';
        statusBarItem.tooltip = 'Harmony bridge stopped — click to start';
        statusBarItem.backgroundColor = new vscode.ThemeColor('statusBarItem.warningBackground');
    }
    statusBarItem.show();
}

export function deactivate() {
    stopBridge();
}
