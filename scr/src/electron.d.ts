export interface SyncResult {
  online: boolean;
  launcherVersion: {
    local: string;
    remote: string;
    needsUpdate: boolean;
  };
  viethoaVersion: {
    installed: string;
    remote: string;
    needsUpdate: boolean;
  };
  bgImageSrc: string;
  bgmSrc: string | null;
  newsText: string;
  noticesText: string;
  banners: Array<{
    id: number;
    image: string;
    tag: string;
    title: string;
    subtitle: string;
  }>;
}

export interface LauncherBridge {
  minimize: () => void;
  close: () => void;
  openRepository: () => Promise<void>;
  openUrl: (url: string) => Promise<void>;
  detectGamePath: () => Promise<string | null>;
  selectFolder: () => Promise<string | null>;
  openFolder: (path: string) => Promise<boolean>;
  launchGame: (path: string) => Promise<{ success: boolean; message: string }>;
  syncAssets: (force?: boolean) => Promise<SyncResult>;
  updateViethoa: (gameDir: string) => Promise<{ success: boolean; version: string; message: string }>;
}

declare global {
  interface Window {
    launcher?: LauncherBridge;
  }
}
