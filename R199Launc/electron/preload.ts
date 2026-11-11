import { contextBridge, ipcRenderer } from 'electron';

contextBridge.exposeInMainWorld('launcher', {
  // Window controls
  minimize: () => ipcRenderer.send('launcher:minimize'),
  close: () => ipcRenderer.send('launcher:close'),
  openRepository: () => ipcRenderer.invoke('launcher:open-repository'),

  // Game management
  detectGame: () => ipcRenderer.invoke('launcher:detect-game'),
  selectGameFolder: () => ipcRenderer.invoke('launcher:select-game-folder'),
  scanGameInstallations: () => ipcRenderer.invoke('launcher:scan-game-installations'),
  setGamePath: (dir: string) => ipcRenderer.invoke('launcher:set-game-path', dir),
  openGameFolder: () => ipcRenderer.invoke('launcher:open-game-folder'),
  openPersistentRoot: () => ipcRenderer.invoke('launcher:open-persistent-root'),
  launchGame: () => ipcRenderer.invoke('launcher:launch-game'),

  // Bluepoch Hotupdate API
  checkBluepochUpdate: (version?: string) => ipcRenderer.invoke('launcher:check-bluepoch-update', version),
  checkBluepochResources: (version?: string) => ipcRenderer.invoke('launcher:check-bluepoch-resources', version),

  // Mod management
  installVietnamese: () => ipcRenderer.invoke('launcher:install-vietnamese'),
  uninstallVietnamese: () => ipcRenderer.invoke('launcher:uninstall-vietnamese'),

  // Step 4: Verification & Maintenance
  verifyIntegrity: () => ipcRenderer.invoke('launcher:verify-integrity'),
  openBackupFolder: () => ipcRenderer.invoke('launcher:open-backup-folder'),
  checkGithubUpdate: () => ipcRenderer.invoke('launcher:check-github-update'),
});
