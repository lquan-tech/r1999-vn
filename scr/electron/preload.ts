import { contextBridge, ipcRenderer } from 'electron';

contextBridge.exposeInMainWorld('launcher', {
  minimize: () => ipcRenderer.send('launcher:minimize'),
  close: () => ipcRenderer.send('launcher:close'),
  openRepository: () => ipcRenderer.invoke('launcher:open-repository'),
  openUrl: (url: string) => ipcRenderer.invoke('launcher:open-url', url),
  detectGamePath: () => ipcRenderer.invoke('launcher:detect-game-path'),
  selectFolder: () => ipcRenderer.invoke('launcher:select-folder'),
  openFolder: (dirPath: string) => ipcRenderer.invoke('launcher:open-folder', dirPath),
  launchGame: (gamePath: string) => ipcRenderer.invoke('launcher:launch-game', gamePath),
  syncAssets: (force?: boolean) => ipcRenderer.invoke('launcher:sync-assets', force),
  updateViethoa: (gameDir: string) => ipcRenderer.invoke('launcher:update-viethoa', gameDir),
});
