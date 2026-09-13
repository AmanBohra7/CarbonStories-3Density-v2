'use strict';

const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('personaDesktop', {
  changeFolder: () => ipcRenderer.invoke('content:change-folder'),
  readConfig: relativePath => ipcRenderer.invoke('config:read', relativePath),
  saveConfig: (relativePath, text) => ipcRenderer.invoke('config:save', relativePath, text),
  loadPersonas: () => ipcRenderer.invoke('personas:load'),
  savePersona: (fileName, text) => ipcRenderer.invoke('personas:save', fileName, text),
  assetExists: relativePath => ipcRenderer.invoke('assets:exists', relativePath),
  assetUrl: relativePath => `streaming-asset://${encodeURI(String(relativePath).replaceAll('\\', '/').replace(/^\/+/, ''))}`,
  replaceAsset: relativePath => ipcRenderer.invoke('assets:replace', relativePath),
  setDirtyState: isDirty => ipcRenderer.send('editor:dirty-state', Boolean(isDirty))
});
