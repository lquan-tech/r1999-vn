import { app, BrowserWindow, ipcMain, shell, dialog, protocol, net, nativeImage } from 'electron';
import path from 'node:path';
import fs from 'node:fs';
import https from 'node:https';
import http from 'node:http';
import { pathToFileURL } from 'node:url';
import { spawn, execSync } from 'node:child_process';
import crypto from 'node:crypto';

// Ensure proper Windows Taskbar grouping and application icon
if (process.platform === 'win32') {
  app.setAppUserModelId('vn.reverse1999.launcher');
}

const REPOSITORY_URL = 'https://github.com/lquan-tech/r1999-vn';
const LAUNCHER_VERSION = '2.3.0';

// Register custom protocol scheme before app is ready
protocol.registerSchemesAsPrivileged([
  {
    scheme: 'r1999-asset',
    privileges: {
      standard: true,
      secure: true,
      supportFetchAPI: true,
      corsEnabled: true,
      stream: true,
      bypassCSP: true,
    },
  },
]);

let mainWindow: BrowserWindow | null = null;
let cacheDir = '';
let gameMonitorTimer: NodeJS.Timeout | null = null;

function getCacheDir(): string {
  if (!cacheDir) {
    cacheDir = path.join(app.getPath('userData'), 'r1999_assets');
    if (!fs.existsSync(cacheDir)) {
      fs.mkdirSync(cacheDir, { recursive: true });
    }
  }
  return cacheDir;
}

function getStoredGameConfigPath(): string {
  return path.join(app.getPath('userData'), 'game_config.json');
}

function getStoredGameDir(): string | null {
  try {
    const cfgPath = getStoredGameConfigPath();
    if (fs.existsSync(cfgPath)) {
      const data = JSON.parse(fs.readFileSync(cfgPath, 'utf8'));
      if (data && typeof data.gameDir === 'string' && data.gameDir.trim().length > 0) {
        return data.gameDir.trim();
      }
    }
  } catch {}
  return null;
}

function saveStoredGameDir(dir: string): void {
  try {
    const cfgPath = getStoredGameConfigPath();
    const parent = path.dirname(cfgPath);
    if (!fs.existsSync(parent)) fs.mkdirSync(parent, { recursive: true });
    fs.writeFileSync(cfgPath, JSON.stringify({ gameDir: dir, updatedAt: new Date().toISOString() }, null, 2), 'utf8');
  } catch (e) {
    console.error('Failed to save game dir config:', e);
  }
}

// Check if a directory is a valid Reverse: 1999 game folder
function isValidGameDir(dir: string): boolean {
  if (!dir || !fs.existsSync(dir)) return false;
  return (
    fs.existsSync(path.join(dir, 'reverse1999.exe')) ||
    fs.existsSync(path.join(dir, 'Reverse1999.exe')) ||
    fs.existsSync(path.join(dir, 'reverse1999_Data'))
  );
}

// Auto-detect the game installation path
function autoDetectGameDirectory(): string | null {
  // 1. Check previously stored path
  const stored = getStoredGameDir();
  if (stored) {
    if (isValidGameDir(stored)) return stored;
    const sub = path.join(stored, 'Reverse1999en');
    if (isValidGameDir(sub)) return sub;
  }

  const candidateDirs: string[] = [];

  // 2. Query Windows Registry (HKCU and HKLM under Software\Bluepoch)
  if (process.platform === 'win32') {
    const bluepochKeys = ['reverse1999en', 'reverse1999jp', 'reverse1999kr', 'reverse1999cn', 'reverse1999tw'];
    for (const sub of bluepochKeys) {
      for (const root of ['HKCU', 'HKLM']) {
        try {
          const out = execSync(`reg query "${root}\\Software\\Bluepoch\\${sub}" /v "game_exe_install_dir" 2>nul`, { encoding: 'utf8' });
          const m = out.match(/game_exe_install_dir\s+REG_SZ\s+(.+)/i);
          if (m && m[1]) candidateDirs.push(m[1].trim());
        } catch {}

        try {
          const out = execSync(`reg query "${root}\\Software\\Bluepoch\\${sub}" /v "laucher_exe_install_dir" 2>nul`, { encoding: 'utf8' });
          const m = out.match(/laucher_exe_install_dir\s+REG_SZ\s+(.+)/i);
          if (m && m[1]) {
            const raw = m[1].trim();
            candidateDirs.push(raw);
            candidateDirs.push(path.join(raw, 'Reverse1999en'));
          }
        } catch {}
      }
    }
  }

  // 3. Common drive letters and default installation folders
  const drives = ['D', 'C', 'E', 'F', 'G'];
  for (const d of drives) {
    candidateDirs.push(`${d}:\\bluepoch\\reverse1999_global\\Reverse1999en`);
    candidateDirs.push(`${d}:\\bluepoch\\reverse1999_global`);
    candidateDirs.push(`${d}:\\Program Files\\Bluepoch\\reverse1999_global\\Reverse1999en`);
    candidateDirs.push(`${d}:\\Program Files\\Bluepoch\\reverse1999_global`);
    candidateDirs.push(`${d}:\\Games\\reverse1999_global\\Reverse1999en`);
    candidateDirs.push(`${d}:\\Games\\reverse1999_global`);
    candidateDirs.push(`${d}:\\Reverse1999\\Reverse1999en`);
    candidateDirs.push(`${d}:\\Reverse1999`);
  }

  // 4. Verify candidate paths
  for (const cand of candidateDirs) {
    if (!cand) continue;
    try {
      if (isValidGameDir(cand)) {
        saveStoredGameDir(cand);
        return cand;
      }
      const sub = path.join(cand, 'Reverse1999en');
      if (isValidGameDir(sub)) {
        saveStoredGameDir(sub);
        return sub;
      }
    } catch {}
  }

  return null;
}

// Robust file download supporting 301/302 redirects
function downloadFile(url: string, destPath: string): Promise<string> {
  return new Promise((resolve, reject) => {
    const getter = url.startsWith('https:') ? https : http;
    const req = getter.get(
      url,
      { headers: { 'User-Agent': 'R1999-VietHoa-Launcher/2.3.0' } },
      (res) => {
        if (res.statusCode && res.statusCode >= 300 && res.statusCode < 400 && res.headers.location) {
          return downloadFile(res.headers.location, destPath).then(resolve).catch(reject);
        }
        if (res.statusCode !== 200) {
          return reject(new Error(`HTTP ${res.statusCode} for ${url}`));
        }
        const fileStream = fs.createWriteStream(destPath);
        res.pipe(fileStream);
        fileStream.on('finish', () => {
          fileStream.close();
          resolve(destPath);
        });
        fileStream.on('error', (err) => {
          try { fs.unlinkSync(destPath); } catch {}
          reject(err);
        });
      }
    );
    req.on('error', (err) => {
      try { fs.unlinkSync(destPath); } catch {}
      reject(err);
    });
  });
}

// Fetch text with timeout
function fetchText(url: string, timeoutMs = 6000): Promise<string> {
  return new Promise((resolve, reject) => {
    const getter = url.startsWith('https:') ? https : http;
    const req = getter.get(
      url,
      { headers: { 'User-Agent': 'R1999-VietHoa-Launcher/2.3.0' } },
      (res) => {
        if (res.statusCode && res.statusCode >= 300 && res.statusCode < 400 && res.headers.location) {
          return fetchText(res.headers.location, timeoutMs).then(resolve).catch(reject);
        }
        if (res.statusCode !== 200) {
          return reject(new Error(`HTTP ${res.statusCode}`));
        }
        let data = '';
        res.setEncoding('utf8');
        res.on('data', (chunk) => { data += chunk; });
        res.on('end', () => resolve(data.trim()));
      }
    );
    req.setTimeout(timeoutMs, () => {
      req.destroy();
      reject(new Error('Timeout'));
    });
    req.on('error', reject);
  });
}

const getAppIcon = () => {
  const possiblePaths = [
    path.join(__dirname, '../public/reverse1999-icon.ico'),
    path.join(__dirname, '../dist/reverse1999-icon.ico'),
    path.join(__dirname, '../public/icon.png'),
    path.join(process.resourcesPath, 'reverse1999-icon.ico'),
    path.join(process.resourcesPath, 'icon.ico'),
    path.join(__dirname, '../public/icon.ico'),
  ];
  for (const p of possiblePaths) {
    if (fs.existsSync(p)) {
      const img = nativeImage.createFromPath(p);
      if (!img.isEmpty()) return { path: p, image: img };
    }
  }
  return null;
};

const createWindow = () => {
  const appIconInfo = getAppIcon();
  const appIcon = appIconInfo?.image;

  mainWindow = new BrowserWindow({
    width: 1280,
    height: 720,
    minWidth: 1280,
    maxWidth: 1280,
    minHeight: 720,
    maxHeight: 720,
    resizable: false,
    maximizable: false,
    minimizable: true,
    fullscreenable: false,
    frame: false,
    show: false,
    backgroundColor: '#0c0f14',
    autoHideMenuBar: true,
    title: 'REVERSE: 1999 VIỆT HÓA',
    icon: appIcon || undefined,
    webPreferences: {
      preload: path.join(__dirname, 'preload.cjs'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: false,
    },
  });

  if (appIcon && !appIcon.isEmpty()) {
    mainWindow.setIcon(appIcon);
  }

  mainWindow.once('ready-to-show', () => {
    mainWindow?.show();
    if (appIcon && !appIcon.isEmpty()) {
      mainWindow?.setIcon(appIcon);
    }
  });

  if (process.env.VITE_DEV_SERVER_URL) {
    mainWindow.loadURL(process.env.VITE_DEV_SERVER_URL);
  } else {
    mainWindow.loadFile(path.join(__dirname, '../dist/index.html'));
  }
};

// Window Controls
ipcMain.on('launcher:minimize', (event) => {
  BrowserWindow.fromWebContents(event.sender)?.minimize();
});

ipcMain.on('launcher:close', (event) => {
  BrowserWindow.fromWebContents(event.sender)?.close();
});

// External Links
ipcMain.handle('launcher:open-repository', () => shell.openExternal(REPOSITORY_URL));
ipcMain.handle('launcher:open-url', (_event, url: string) => {
  if (url && (url.startsWith('https://') || url.startsWith('http://'))) {
    return shell.openExternal(url);
  }
});

// Auto-detect game directory
ipcMain.handle('launcher:detect-game-path', async () => {
  return autoDetectGameDirectory();
});

// Folder Dialog & Operations
ipcMain.handle('launcher:select-folder', async () => {
  if (!mainWindow) return null;
  const result = await dialog.showOpenDialog(mainWindow, {
    title: 'Chọn thư mục cài đặt Reverse: 1999',
    properties: ['openDirectory'],
  });
  if (!result.canceled && result.filePaths.length > 0) {
    let chosen = result.filePaths[0];
    // Smart-check: If user selected reverse1999_global instead of Reverse1999en
    const sub = path.join(chosen, 'Reverse1999en');
    if (!isValidGameDir(chosen) && isValidGameDir(sub)) {
      chosen = sub;
    }
    saveStoredGameDir(chosen);
    return chosen;
  }
  return null;
});

ipcMain.handle('launcher:open-folder', async (_event, folderPath: string) => {
  try {
    if (folderPath && fs.existsSync(folderPath)) {
      await shell.openPath(folderPath);
      return true;
    }
  } catch (err) {
    console.error('Failed to open folder:', err);
  }
  return false;
});

// Game Launcher Execution using ShellExecuteEx with Administrator Elevation & WorkingDir
ipcMain.handle('launcher:launch-game', async (_event, gameDir: string) => {
  try {
    if (!gameDir || !fs.existsSync(gameDir)) {
      return { success: false, message: 'Chưa cấu hình hoặc không tìm thấy thư mục game!' };
    }

    const candidates = [
      path.join(gameDir, 'reverse1999.exe'),
      path.join(gameDir, 'Reverse1999.exe'),
      path.join(gameDir, 'Reverse1999en', 'reverse1999.exe'),
      path.join(gameDir, 'reverse1999-launcher.exe'),
      path.join(gameDir, 'game', 'Reverse1999.exe'),
    ];

    let targetExe: string | null = null;
    for (const c of candidates) {
      if (fs.existsSync(c)) {
        targetExe = c;
        break;
      }
    }

    if (!targetExe) {
      await shell.openPath(gameDir);
      return { success: false, message: 'Không tìm thấy file reverse1999.exe trong thư mục được chọn.' };
    }

    const workingDir = path.dirname(targetExe);

    // Locate helper game_starter.exe
    const starterCandidates = [
      path.join(process.resourcesPath, 'game_starter.exe'),
      path.join(__dirname, '../resources/game_starter.exe'),
      path.join(__dirname, '../public/game_starter.exe'),
      path.join(__dirname, 'game_starter.exe'),
    ];

    let starterExe: string | null = null;
    for (const s of starterCandidates) {
      if (fs.existsSync(s)) {
        starterExe = s;
        break;
      }
    }

    if (starterExe) {
      // Spawn game_starter.exe with exact target executable path
      const child = spawn(starterExe, [targetExe], {
        detached: true,
        stdio: 'ignore',
        windowsHide: true,
      });
      child.unref();
    } else {
      // Fallback: Use PowerShell Start-Process with -Verb RunAs for UAC elevation & ShellExecute
      const psCmd = `Start-Process -FilePath '${targetExe.replace(/'/g, "''")}' -WorkingDirectory '${workingDir.replace(/'/g, "''")}' -Verb RunAs`;
      const child = spawn('powershell.exe', ['-NoProfile', '-WindowStyle', 'Hidden', '-Command', psCmd], {
        detached: true,
        stdio: 'ignore',
        windowsHide: true,
      });
      child.unref();
    }

    // Official launcher behavior: Minimize window and monitor game process
    if (mainWindow && !mainWindow.isDestroyed()) {
      mainWindow.minimize();
    }

    // Setup monitor timer to restore launcher when game closes
    if (gameMonitorTimer) clearInterval(gameMonitorTimer);
    const exeNameOnly = path.basename(targetExe, path.extname(targetExe));
    let hasBeenRunning = false;

    gameMonitorTimer = setInterval(() => {
      try {
        const out = execSync(`tasklist /fi "IMAGENAME eq ${path.basename(targetExe)}" 2>nul`, { encoding: 'utf8' });
        const isRunning = out.toLowerCase().includes(path.basename(targetExe).toLowerCase());

        if (isRunning) {
          hasBeenRunning = true;
        } else if (hasBeenRunning) {
          // Game exited, restore launcher
          if (gameMonitorTimer) {
            clearInterval(gameMonitorTimer);
            gameMonitorTimer = null;
          }
          if (mainWindow && !mainWindow.isDestroyed()) {
            mainWindow.restore();
            mainWindow.show();
            mainWindow.focus();
          }
        }
      } catch {}
    }, 4000);

    return { success: true, message: `Đang khởi chạy Reverse: 1999...` };
  } catch (err: any) {
    return { success: false, message: err?.message || 'Lỗi khi khởi chạy game' };
  }
});

// Sync Assets & Version Checks
ipcMain.handle('launcher:sync-assets', async (_event, force = false) => {
  const cDir = getCacheDir();
  const manifestPath = path.join(cDir, 'manifest.json');
  const installedVersionPath = path.join(cDir, 'installed_viethoa.json');

  let installedViethoaVersion = '0.0.0';
  if (fs.existsSync(installedVersionPath)) {
    try {
      const data = JSON.parse(fs.readFileSync(installedVersionPath, 'utf8'));
      const activeGameDir = data.gameDir || autoDetectGameDirectory();
      if (data.version && data.fileHashes && activeGameDir) {
        const perRoot = path.join(activeGameDir, 'reverse1999_Data', 'StreamingAssets', 'PersistentRoot');
        let allValid = true;
        for (const [relPath, expectedMd5] of Object.entries(data.fileHashes as Record<string, string>)) {
          const targetFile = path.join(perRoot, relPath.replace(/\//g, path.sep));
          if (!fs.existsSync(targetFile) || md5FileSync(targetFile).toLowerCase() !== String(expectedMd5).toLowerCase()) {
            allValid = false;
            break;
          }
        }
        if (allValid) {
          installedViethoaVersion = data.version;
        }
      }
    } catch {}
  }

  let cachedManifest = { cachedViethoaVersion: '', lastSync: 0 };
  if (fs.existsSync(manifestPath)) {
    try {
      cachedManifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
    } catch {}
  }

  let remoteLauncherVersion = LAUNCHER_VERSION;
  let remoteViethoaVersion = '1.0.0';
  let online = false;

  const now = Date.now();
  const isFresh = now - (cachedManifest.lastSync || 0) < 60000;

  if (force || !isFresh) {
    try {
      const [rLauncher, rViethoa] = await Promise.allSettled([
        fetchText(`https://raw.githubusercontent.com/lquan-tech/r1999-vn/main/public/launcherversion.txt?t=${now}`),
        fetchText(`https://raw.githubusercontent.com/lquan-tech/r1999-vn/main/public/viethoaversion.txt?t=${now}`),
      ]);

      if (rLauncher.status === 'fulfilled' && rLauncher.value) {
        remoteLauncherVersion = rLauncher.value;
        online = true;
      }
      if (rViethoa.status === 'fulfilled' && rViethoa.value) {
        remoteViethoaVersion = rViethoa.value;
        online = true;
      }

      if (online) {
        fs.writeFileSync(
          manifestPath,
          JSON.stringify({ cachedViethoaVersion: remoteViethoaVersion, lastSync: now }, null, 2),
          'utf8'
        );
      }
    } catch (e) {
      console.warn('Sync assets fetch failed:', e);
    }
  } else {
    remoteViethoaVersion = cachedManifest.cachedViethoaVersion || '1.0.0';
  }

  // Download additional UI assets from repo
  const assetsToSync = [
    { name: 'bgimage.png', url: 'https://raw.githubusercontent.com/lquan-tech/r1999-vn/main/public/image/bgimage.png' },
    { name: 'BGM.mp3', url: 'https://raw.githubusercontent.com/lquan-tech/r1999-vn/main/public/audio/BGM.mp3' },
    { name: 'banner1.png', url: 'https://raw.githubusercontent.com/lquan-tech/r1999-vn/main/public/image/banner1.png' },
    { name: 'banner2.png', url: 'https://raw.githubusercontent.com/lquan-tech/r1999-vn/main/public/image/banner2.png' },
    { name: 'banner3.png', url: 'https://raw.githubusercontent.com/lquan-tech/r1999-vn/main/public/image/banner3.png' },
    { name: 'news.txt', url: 'https://raw.githubusercontent.com/lquan-tech/r1999-vn/main/public/news/news.txt' },
    { name: 'notices.txt', url: 'https://raw.githubusercontent.com/lquan-tech/r1999-vn/main/public/news/notices.txt' },
  ];

  await Promise.allSettled(
    assetsToSync.map(async (item) => {
      const dest = path.join(cDir, item.name);
      if (force || !fs.existsSync(dest)) {
        try {
          await downloadFile(`${item.url}?t=${Date.now()}`, dest);
        } catch {}
      }
    })
  );

  const readCachedText = (fileName: string, fallback: string): string => {
    const fPath = path.join(cDir, fileName);
    if (fs.existsSync(fPath)) {
      try {
        return fs.readFileSync(fPath, 'utf8').trim();
      } catch {}
    }
    return fallback;
  };

  const text1 = readCachedText('banner1text.txt', 'Bản Dịch Toàn Bộ Cốt Truyện');
  const text2 = readCachedText('banner2text.txt', 'Sự Kiện UTTU & Bản Cập Nhật Mới');
  const text3 = readCachedText('banner3text.txt', 'Tối Ưu Hóa Trải Nghiệm Tiếng Việt');
  const newsRaw = readCachedText('news.txt', 'haha tree 3');
  const noticesRaw = readCachedText('notices.txt', 'haha one1');

  const bgImageExists = fs.existsSync(path.join(cDir, 'bgimage.png'));
  const bgmExists = fs.existsSync(path.join(cDir, 'BGM.mp3'));

  return {
    online,
    launcherVersion: {
      local: LAUNCHER_VERSION,
      remote: remoteLauncherVersion,
      needsUpdate: remoteLauncherVersion !== LAUNCHER_VERSION,
    },
    viethoaVersion: {
      installed: installedViethoaVersion,
      remote: remoteViethoaVersion,
      needsUpdate: installedViethoaVersion !== remoteViethoaVersion,
    },
    bgImageSrc: bgImageExists ? `r1999-asset://bgimage.png?t=${Date.now()}` : './mainwindow_en.png',
    bgmSrc: bgmExists ? `r1999-asset://BGM.mp3?t=${Date.now()}` : null,
    newsText: newsRaw,
    noticesText: noticesRaw,
    banners: [
      {
        id: 1,
        image: fs.existsSync(path.join(cDir, 'banner1.png')) ? `r1999-asset://banner1.png?t=${Date.now()}` : './mainwindow_en.jpg',
        tag: 'BẢN DỊCH ARCANIST',
        title: text1,
        subtitle: 'Bản dịch toàn bộ cốt truyện & phân cảnh',
      },
      {
        id: 2,
        image: fs.existsSync(path.join(cDir, 'banner2.png')) ? `r1999-asset://banner2.png?t=${Date.now()}` : './mainwindow_en.jpg',
        tag: 'SỰ KIỆN ĐẶC BIỆT',
        title: text2,
        subtitle: 'Nội dung cốt truyện mới & tối ưu phông chữ',
      },
      {
        id: 3,
        image: fs.existsSync(path.join(cDir, 'banner3.png')) ? `r1999-asset://banner3.png?t=${Date.now()}` : './mainwindow_en.jpg',
        tag: 'HƯỚNG DẪN',
        title: text3,
        subtitle: 'Kiểm tra tính toàn vẹn và sửa lỗi bản dịch',
      },
    ],
  };
});

interface ViethoaManifestEntry {
  repoPath: string;
  targetRelPath: string;
  localhashKey?: string;
  md5?: string;
}

function md5FileSync(filePath: string): string {
  const hash = crypto.createHash('md5');
  hash.update(fs.readFileSync(filePath));
  return hash.digest('hex');
}

// Install / Update Viethoa directly from public/fileviethoa folder on GitHub
ipcMain.handle('launcher:update-viethoa', async (_event, gameDir: string) => {
  if (!gameDir || !fs.existsSync(gameDir)) {
    return { success: false, version: '0.0.0', message: 'Vui lòng chọn thư mục cài đặt game trước khi tải bản dịch!' };
  }

  const persistentRoot = path.join(gameDir, 'reverse1999_Data', 'StreamingAssets', 'PersistentRoot');
  if (!fs.existsSync(persistentRoot)) {
    return { success: false, version: '0.0.0', message: 'Không tìm thấy PersistentRoot trong thư mục game!' };
  }

  const cDir = getCacheDir();
  const installedVersionPath = path.join(cDir, 'installed_viethoa.json');
  const stageDir = path.join(cDir, 'viethoa_stage');
  if (!fs.existsSync(stageDir)) fs.mkdirSync(stageDir, { recursive: true });

  let remoteVersion = '1.1.0';
  const ts = Date.now();
  try {
    const v = await fetchText(`https://raw.githubusercontent.com/lquan-tech/r1999-vn/main/public/viethoaversion.txt?t=${ts}`);
    if (v) remoteVersion = v;
  } catch {}

  let manifestEntries: ViethoaManifestEntry[] = [];
  try {
    const rawManifest = await fetchText(
      `https://raw.githubusercontent.com/lquan-tech/r1999-vn/main/public/fileviethoa/manifest.json?t=${ts}`,
      12000
    );
    const parsed = JSON.parse(rawManifest);
    if (parsed && Array.isArray(parsed.files)) {
      manifestEntries = parsed.files;
      if (parsed.version) remoteVersion = String(parsed.version);
    }
  } catch (err) {
    console.warn('Could not load public/fileviethoa/manifest.json:', err);
  }

  if (manifestEntries.length === 0) {
    return { success: false, version: remoteVersion, message: 'Không thể tải manifest bản Việt hóa từ GitHub.' };
  }

  // 1. Download all patch files into userData staging directory
  const stagedCopies: Array<{ src: string; dest: string; localhashKey?: string; md5: string }> = [];
  for (const entry of manifestEntries) {
    const relNorm = entry.targetRelPath.replace(/\//g, path.sep);
    const stagedPath = path.join(stageDir, relNorm);
    const stagedParent = path.dirname(stagedPath);
    if (!fs.existsSync(stagedParent)) fs.mkdirSync(stagedParent, { recursive: true });

    const rawUrl = `https://raw.githubusercontent.com/lquan-tech/r1999-vn/main/${entry.repoPath}?t=${ts}`;
    try {
      await downloadFile(rawUrl, stagedPath);
      const actualMd5 = md5FileSync(stagedPath);
      if (entry.md5 && actualMd5.toLowerCase() !== entry.md5.toLowerCase()) {
        return { success: false, version: remoteVersion, message: `Lỗi kiểm tra MD5 cho ${entry.targetRelPath}` };
      }
      stagedCopies.push({
        src: stagedPath,
        dest: path.join(persistentRoot, relNorm),
        localhashKey: entry.localhashKey,
        md5: actualMd5,
      });
    } catch (e: any) {
      return { success: false, version: remoteVersion, message: `Lỗi tải ${entry.targetRelPath}: ${e?.message || e}` };
    }
  }

  // 2. Prepare updated localhash in staging if localhash exists
  const localhashPath = path.join(persistentRoot, 'localhash');
  const stagedLocalhash = path.join(stageDir, 'localhash.patched');
  let hasLocalhashUpdate = false;
  if (fs.existsSync(localhashPath)) {
    try {
      const lhData = JSON.parse(fs.readFileSync(localhashPath, 'utf8'));
      for (const item of stagedCopies) {
        if (item.localhashKey) {
          lhData[item.localhashKey] = item.md5;
        }
      }
      fs.writeFileSync(stagedLocalhash, JSON.stringify(lhData), 'utf8');
      hasLocalhashUpdate = true;
    } catch (e) {
      console.warn('Could not update localhash:', e);
    }
  }

  // 3. Copy into PersistentRoot (with automatic UAC elevation fallback for Program Files)
  try {
    for (const item of stagedCopies) {
      const destParent = path.dirname(item.dest);
      if (!fs.existsSync(destParent)) fs.mkdirSync(destParent, { recursive: true });
      fs.copyFileSync(item.src, item.dest);
    }
    if (hasLocalhashUpdate) {
      fs.copyFileSync(stagedLocalhash, localhashPath);
    }
  } catch (err: any) {
    if (process.platform === 'win32' && (err?.code === 'EPERM' || err?.code === 'EACCES')) {
      const psLines: string[] = [];
      for (const item of stagedCopies) {
        const dParent = path.dirname(item.dest).replace(/'/g, "''");
        const sFile = item.src.replace(/'/g, "''");
        const dFile = item.dest.replace(/'/g, "''");
        psLines.push(`New-Item -ItemType Directory -Force -Path '${dParent}' | Out-Null`);
        psLines.push(`Copy-Item -Force '${sFile}' '${dFile}'`);
      }
      if (hasLocalhashUpdate) {
        psLines.push(`Copy-Item -Force '${stagedLocalhash.replace(/'/g, "''")}' '${localhashPath.replace(/'/g, "''")}'`);
      }
      const scriptPath = path.join(stageDir, 'apply_patch_admin.ps1');
      fs.writeFileSync(scriptPath, psLines.join('\r\n'), 'utf8');
      try {
        execSync(
          `powershell -NoProfile -Command "Start-Process powershell.exe -ArgumentList '-NoProfile -ExecutionPolicy Bypass -File \\"${scriptPath}\\"' -Verb RunAs -Wait -WindowStyle Hidden"`,
          { encoding: 'utf8' }
        );
      } catch (elevErr: any) {
        return { success: false, version: remoteVersion, message: `Cần quyền Administrator để ghi vào thư mục game: ${elevErr?.message || elevErr}` };
      }
    } else {
      return { success: false, version: remoteVersion, message: `Lỗi ghi file vào game: ${err?.message || err}` };
    }
  }

  // Save new installed version & verified file hashes
  const fileHashes: Record<string, string> = {};
  for (const item of stagedCopies) {
    if (item.localhashKey) fileHashes[item.localhashKey] = item.md5;
  }
  try {
    fs.writeFileSync(
      installedVersionPath,
      JSON.stringify({ version: remoteVersion, installedAt: new Date().toISOString(), gameDir, downloadedCount: stagedCopies.length, fileHashes }, null, 2),
      'utf8'
    );
  } catch {}

  return {
    success: true,
    version: remoteVersion,
    message: `Đã cập nhật bản dịch & font Lora/JetBrains Mono v${remoteVersion}`,
  };
});

app.whenReady().then(() => {
  const cDir = getCacheDir();

  // Register r1999-asset protocol to stream cached files directly
  protocol.handle('r1999-asset', (request) => {
    const cleanUrl = request.url.replace(/^r1999-asset:\/\//, '').split('?')[0];
    const fileName = path.basename(decodeURIComponent(cleanUrl.replace(/^\/+/, '')));
    const filePath = path.join(cDir, fileName);

    if (fs.existsSync(filePath)) {
      return net.fetch(pathToFileURL(filePath).toString());
    }

    const publicPath = path.join(__dirname, '../public', fileName);
    if (fs.existsSync(publicPath)) {
      return net.fetch(pathToFileURL(publicPath).toString());
    }

    return new Response('Not found', { status: 404 });
  });

  function ensureStartMenuShortcut() {
    if (process.platform !== 'win32') return;
    try {
      const startMenu = path.join(app.getPath('appData'), 'Microsoft/Windows/Start Menu/Programs');
      const shortcutPath = path.join(startMenu, 'Reverse 1999 Việt Hóa Launcher.lnk');
      const iconInfo = getAppIcon();
      const iconPath = iconInfo?.path || path.join(process.resourcesPath, 'reverse1999-icon.ico');

      shell.writeShortcutLink(shortcutPath, 'create', {
        target: process.execPath,
        args: app.isPackaged ? '' : `"${app.getAppPath()}"`,
        description: 'Reverse: 1999 Việt Hóa Launcher',
        icon: iconPath,
        iconIndex: 0,
        appUserModelId: 'vn.reverse1999.launcher',
      });
    } catch (err) {
      console.warn('Could not register shortcut:', err);
    }
  }

  ensureStartMenuShortcut();
  createWindow();

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) createWindow();
  });
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') app.quit();
});
