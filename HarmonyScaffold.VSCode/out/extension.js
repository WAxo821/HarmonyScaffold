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
exports.activate = activate;
exports.deactivate = deactivate;
const vscode = __importStar(require("vscode"));
const cli_1 = require("./cli");
const bridge_1 = require("./bridge");
const hotreload_1 = require("./hotreload");
let statusBarItem;
let hotReloadStatusItem;
function activate(context) {
    const outputChannel = vscode.window.createOutputChannel('Harmony Scaffold');
    context.subscriptions.push(outputChannel);
    // Status bar
    statusBarItem = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Right, 100);
    statusBarItem.command = 'harmony-scaffold.toggleBridge';
    context.subscriptions.push(statusBarItem);
    updateStatusBar();
    // ---- init ----
    context.subscriptions.push(vscode.commands.registerCommand('harmony-scaffold.init', async (uri) => {
        const name = await vscode.window.showInputBox({
            prompt: 'Plugin name',
            placeHolder: 'MyPlugin',
            validateInput: (v) => v ? null : 'Name is required'
        });
        if (!name) {
            return;
        }
        const folder = uri?.fsPath ?? vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;
        if (!folder) {
            vscode.window.showErrorMessage('No folder selected. Right-click a folder in Explorer or open a workspace.');
            return;
        }
        const result = await (0, cli_1.runCli)(['init', '--name', name, '--output', folder]);
        if (result.success) {
            const data = result.data;
            outputChannel.appendLine(`Project created: ${data.path}`);
            outputChannel.show();
            vscode.window.showInformationMessage(`BepInEx plugin "${name}" initialized!`);
        }
        else {
            vscode.window.showErrorMessage(result.error ?? 'Unknown error');
        }
    }));
    // ---- generate ----
    context.subscriptions.push(vscode.commands.registerCommand('harmony-scaffold.generate', async () => {
        const className = await vscode.window.showInputBox({
            prompt: 'Target class name',
            placeHolder: 'Player',
            validateInput: (v) => v ? null : 'Class name is required'
        });
        if (!className) {
            return;
        }
        const methodName = await vscode.window.showInputBox({
            prompt: 'Target method name',
            placeHolder: 'TakeDamage',
            validateInput: (v) => v ? null : 'Method name is required'
        });
        if (!methodName) {
            return;
        }
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
        if (!patchType) {
            return;
        }
        const patchTypeMap = {
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
        if (author) {
            args.push('--author', author);
        }
        if (workspaceFolder) {
            args.push('--output', workspaceFolder);
        }
        if (params) {
            args.push('--params', params);
        }
        if (isStatic === 'Yes') {
            args.push('--static');
        }
        if (useState === 'Yes') {
            args.push('--state');
        }
        const result = await (0, cli_1.runCli)(args);
        if (result.success) {
            const data = result.data;
            if (data.path) {
                outputChannel.appendLine(`Patch generated: ${data.path}`);
                outputChannel.show();
                const doc = await vscode.workspace.openTextDocument(data.path);
                await vscode.window.showTextDocument(doc);
            }
            else {
                outputChannel.appendLine('Patch generated (no output path specified).');
                outputChannel.show();
            }
            vscode.window.showInformationMessage('Harmony patch generated!');
        }
        else {
            vscode.window.showErrorMessage(result.error ?? 'Generation failed');
        }
    }));
    // ---- bridge ----
    context.subscriptions.push(vscode.commands.registerCommand('harmony-scaffold.startBridge', () => {
        const workspaceFolder = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath || '.';
        (0, bridge_1.startBridge)(workspaceFolder, async (filePath) => {
            const doc = await vscode.workspace.openTextDocument(filePath);
            await vscode.window.showTextDocument(doc);
        }, (msg) => { outputChannel.appendLine(`[Bridge ERROR] ${msg}`); outputChannel.show(); }, (msg) => { outputChannel.appendLine(`[Bridge] ${msg}`); }, () => { updateStatusBar(); });
        updateStatusBar();
        vscode.window.showInformationMessage('Harmony bridge started on port 5566');
    }));
    context.subscriptions.push(vscode.commands.registerCommand('harmony-scaffold.stopBridge', () => {
        (0, bridge_1.stopBridge)();
        updateStatusBar();
        vscode.window.showInformationMessage('Harmony bridge stopped');
    }));
    context.subscriptions.push(vscode.commands.registerCommand('harmony-scaffold.toggleBridge', () => {
        const status = (0, bridge_1.getStatus)();
        if (status.running) {
            vscode.commands.executeCommand('harmony-scaffold.stopBridge');
        }
        else {
            vscode.commands.executeCommand('harmony-scaffold.startBridge');
        }
    }));
    // ---- hot reload ----
    hotReloadStatusItem = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Right, 99);
    hotReloadStatusItem.command = 'harmony-scaffold.toggleHotReload';
    hotReloadStatusItem.text = '$(debug-start) HotReload';
    hotReloadStatusItem.tooltip = 'HotReload disabled — click to enable (auto-inject on save)';
    hotReloadStatusItem.show();
    context.subscriptions.push(hotReloadStatusItem);
    context.subscriptions.push(vscode.commands.registerCommand('harmony-scaffold.enableHotReload', () => {
        (0, hotreload_1.enableHotReload)(outputChannel, hotReloadStatusItem);
        hotReloadStatusItem.text = '$(zap) HotReload';
        hotReloadStatusItem.tooltip = `HotReload enabled on :${(0, hotreload_1.getPort)()} — save a .cs patch file to auto-inject`;
        hotReloadStatusItem.backgroundColor = undefined;
        vscode.window.showInformationMessage('Hot reload enabled — save .cs patch files to auto-inject');
    }));
    context.subscriptions.push(vscode.commands.registerCommand('harmony-scaffold.disableHotReload', () => {
        (0, hotreload_1.disableHotReload)();
        hotReloadStatusItem.text = '$(debug-start) HotReload';
        hotReloadStatusItem.tooltip = 'HotReload disabled — click to enable';
        hotReloadStatusItem.backgroundColor = new vscode.ThemeColor('statusBarItem.warningBackground');
        vscode.window.showInformationMessage('Hot reload disabled');
    }));
    context.subscriptions.push(vscode.commands.registerCommand('harmony-scaffold.toggleHotReload', () => {
        if ((0, hotreload_1.isActive)()) {
            vscode.commands.executeCommand('harmony-scaffold.disableHotReload');
        }
        else {
            vscode.commands.executeCommand('harmony-scaffold.enableHotReload');
        }
    }));
}
function updateStatusBar() {
    const status = (0, bridge_1.getStatus)();
    if (status.running) {
        statusBarItem.text = '$(radio-tower) Harmony Bridge';
        statusBarItem.tooltip = 'Harmony bridge running on :5566 — click to stop';
        statusBarItem.backgroundColor = undefined;
    }
    else {
        statusBarItem.text = '$(circle-slash) Harmony Bridge';
        statusBarItem.tooltip = 'Harmony bridge stopped — click to start';
        statusBarItem.backgroundColor = new vscode.ThemeColor('statusBarItem.warningBackground');
    }
    statusBarItem.show();
}
function deactivate() {
    (0, bridge_1.stopBridge)();
}
//# sourceMappingURL=extension.js.map