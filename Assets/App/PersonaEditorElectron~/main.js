'use strict';

const { app, BrowserWindow, dialog, ipcMain, protocol, net } = require('electron');
const fs = require('node:fs/promises');
const path = require('node:path');
const { pathToFileURL } = require('node:url');

const PERSONA_COUNT = 6;
let streamingAssetsRoot;
let mainWindow;
let allowClose = false;
let hasUnsavedChanges = false;

const hasSingleInstanceLock = app.requestSingleInstanceLock();
if (!hasSingleInstanceLock) app.quit();

function folderSettingsPath() {
  return path.join(app.getPath('userData'), 'content-folder.json');
}

async function isStreamingAssetsRoot(root) {
  if (typeof root !== 'string' || !root || !path.isAbsolute(root)) return false;
  try { return (await fs.stat(path.join(root, 'Personas'))).isDirectory(); }
  catch { return false; }
}

async function rememberStreamingAssetsRoot(root) {
  try {
    await fs.mkdir(app.getPath('userData'), { recursive: true });
    await fs.writeFile(folderSettingsPath(), JSON.stringify({ streamingAssetsRoot: root }), 'utf8');
  } catch (error) { console.warn('Could not remember content folder:', error.message); }
}

async function restoreStreamingAssetsRoot() {
  try {
    const settings = JSON.parse(await fs.readFile(folderSettingsPath(), 'utf8'));
    if (await isStreamingAssetsRoot(settings.streamingAssetsRoot)) return settings.streamingAssetsRoot;
  } catch { /* Ask for a folder when settings are missing or invalid. */ }
  const root = await selectStreamingAssetsRoot();
  if (root) await rememberStreamingAssetsRoot(root);
  return root;
}

async function selectStreamingAssetsRoot() {
  while (true) {
    const selection = await dialog.showOpenDialog({
      title: 'Select the StreamingAssets folder to edit',
      buttonLabel: 'Use StreamingAssets',
      defaultPath: streamingAssetsRoot,
      properties: ['openDirectory']
    });
    if (selection.canceled || !selection.filePaths[0]) return null;
    const selectedRoot = path.resolve(selection.filePaths[0]);
    try {
      const personas = await fs.stat(path.join(selectedRoot, 'Personas'));
      if (!personas.isDirectory()) throw new Error('Personas must be a folder.');
      return selectedRoot;
    } catch {
      dialog.showErrorBox('Invalid StreamingAssets folder',
        'Select the StreamingAssets folder containing the Personas folder. You can select Assets/StreamingAssets in the Unity project or the StreamingAssets folder in a game build.');
    }
  }
}

function personasRoot() {
  return path.join(streamingAssetsRoot, 'Personas');
}

function safeAssetPath(relativePath) {
  if (typeof relativePath !== 'string' || !relativePath.trim()) throw new Error('The asset path is empty.');
  const normalized = relativePath.replaceAll('\\', '/').replace(/^\/+/, '');
  const resolved = path.resolve(streamingAssetsRoot, ...normalized.split('/'));
  const relative = path.relative(streamingAssetsRoot, resolved);
  if (relative.startsWith('..') || path.isAbsolute(relative)) throw new Error('Asset paths must stay inside StreamingAssets.');
  return resolved;
}

async function createWindow() {
  const window = new BrowserWindow({
    width: 1440,
    height: 940,
    minWidth: 900,
    minHeight: 650,
    backgroundColor: '#0d0d0d',
    autoHideMenuBar: true,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true
    }
  });
  mainWindow = window;

  window.on('close', event => {
    if (allowClose || !hasUnsavedChanges) return;
    event.preventDefault();
    const choice = dialog.showMessageBoxSync(window, {
      type: 'warning',
      title: 'Unsaved persona changes',
      message: 'Close Persona Loader without saving your changes?',
      detail: 'Choose Cancel, save your changes, and then close the editor.',
      buttons: ['Cancel', 'Close without saving'],
      defaultId: 0,
      cancelId: 0,
      noLink: true
    });
    if (choice === 1) {
      allowClose = true;
      window.close();
    }
  });

  window.on('closed', () => { if (mainWindow === window) mainWindow = null; });

  const htmlPath = path.join(__dirname, 'persona-editor.html');
  await window.loadFile(htmlPath);
}

protocol.registerSchemesAsPrivileged([
  { scheme: 'streaming-asset', privileges: { standard: true, secure: true, supportFetchAPI: true, stream: true } }
]);

app.on('second-instance', () => {
  if (!mainWindow) return;
  if (mainWindow.isMinimized()) mainWindow.restore();
  mainWindow.show();
  mainWindow.focus();
});

app.whenReady().then(async () => {
  streamingAssetsRoot = await restoreStreamingAssetsRoot();
  if (!streamingAssetsRoot) {
    app.quit();
    return;
  }
  ipcMain.handle('content:change-folder', async () => {
    const root = await selectStreamingAssetsRoot();
    if (!root || root === streamingAssetsRoot) return { changed: false };
    if (hasUnsavedChanges) {
      const choice = await dialog.showMessageBox(mainWindow, {
        type: 'warning',
        title: 'Unsaved changes',
        message: 'Changing folders will discard unsaved changes.',
        buttons: ['Cancel', 'Discard and change folder'],
        defaultId: 0,
        cancelId: 0
      });
      if (choice.response !== 1) return { changed: false };
    }
    streamingAssetsRoot = root;
    hasUnsavedChanges = false;
    await rememberStreamingAssetsRoot(root);
    return { changed: true, root };
  });
  protocol.handle('streaming-asset', request => {
    const url = new URL(request.url);
    const relativePath = decodeURIComponent(`${url.hostname}${url.pathname}`);
    return net.fetch(pathToFileURL(safeAssetPath(relativePath)).toString());
  });

  ipcMain.handle('personas:load', async () => {
    const results = [];
    for (let index = 0; index < PERSONA_COUNT; index++) {
      const fileName = `persona_${index + 1}.json`;
      try {
        results.push({ fileName, text: await fs.readFile(path.join(personasRoot(), fileName), 'utf8') });
      } catch (error) {
        if (error.code === 'ENOENT') results.push({ fileName, text: null });
        else throw error;
      }
    }
    return { root: streamingAssetsRoot, personas: results };
  });

  ipcMain.handle('personas:save', async (_event, fileName, text) => {
    if (!/^persona_[1-6]\.json$/.test(fileName)) throw new Error('Invalid persona filename.');
    await fs.mkdir(personasRoot(), { recursive: true });
    await fs.writeFile(path.join(personasRoot(), fileName), text, 'utf8');
  });

  function configPath(relativePath) {
    if (!['config.json', 'Results/descriptions.json'].includes(relativePath)) throw new Error('Invalid config path.');
    return safeAssetPath(relativePath);
  }
  ipcMain.handle('config:read', async (_event, relativePath) => {
    try { return await fs.readFile(configPath(relativePath), 'utf8'); }
    catch (error) { if (error.code === 'ENOENT') return null; throw error; }
  });
  ipcMain.handle('config:save', async (_event, relativePath, text) => {
    const destination = configPath(relativePath); JSON.parse(text);
    await fs.mkdir(path.dirname(destination), { recursive: true });
    await fs.writeFile(destination, text, 'utf8');
  });

  ipcMain.handle('assets:exists', async (_event, relativePath) => {
    try { await fs.access(safeAssetPath(relativePath)); return true; }
    catch { return false; }
  });

  ipcMain.on('editor:dirty-state', (_event, isDirty) => {
    hasUnsavedChanges = Boolean(isDirty);
  });

  ipcMain.handle('assets:replace', async (_event, relativePath) => {
    const destination = safeAssetPath(relativePath);
    const extension = path.extname(destination).toLowerCase();
    const result = await dialog.showOpenDialog({
      title: `Replace ${path.basename(destination)}`,
      properties: ['openFile'],
      filters: [
        { name: 'Media', extensions: ['png', 'jpg', 'jpeg', 'webp', 'gif', 'bmp', 'mp4', 'webm', 'mov', 'm4v'] },
        { name: 'Same file type', extensions: extension ? [extension.slice(1)] : ['*'] }
      ]
    });
    if (result.canceled || !result.filePaths[0]) return { replaced: false };
    const source = result.filePaths[0];
    if (extension && path.extname(source).toLowerCase() !== extension) {
      throw new Error(`Choose a ${extension} file to preserve the existing asset path.`);
    }
    await fs.mkdir(path.dirname(destination), { recursive: true });
    await fs.copyFile(source, destination);
    return { replaced: true };
  });

  await createWindow();
  app.on('activate', () => { if (BrowserWindow.getAllWindows().length === 0) createWindow(); });
}).catch(error => {
  dialog.showErrorBox('Could not open Persona Loader', error.message);
  app.quit();
});

app.on('window-all-closed', () => { if (process.platform !== 'darwin') app.quit(); });
