import { app, BrowserWindow, ipcMain, shell, dialog } from 'electron';
import path from 'node:path';
import fs from 'node:fs';
import crypto from 'node:crypto';
import { spawn, execFile, exec } from 'node:child_process';

// Chặn hiển thị popup lỗi JavaScript thô của Electron ra màn hình
process.on('uncaughtException', (err) => {
  console.error('[viLauncher] Uncaught Exception:', err);
});
process.on('unhandledRejection', (reason) => {
  console.error('[viLauncher] Unhandled Rejection:', reason);
});

const REPOSITORY_URL = 'https://github.com/lquan-tech/viLauncher';

const MOD_CONTAINER_NAME = '6ac5a62c64b72b07e9383583eef5c3ac.dat';
const MOD_BUNDLE_NAME = '7335e2dfbf8ec0e4389bc697130b63e2.dat';

// Typical relative install locations, checked on every existing drive letter.
const COMMON_RELATIVE_PATHS = [
  'Program Files (x86)\\reverse1999_global\\Reverse1999en',
  'Program Files\\reverse1999_global\\Reverse1999en',
  'bluepoch\\reverse1999_global\\Reverse1999en',
  'reverse1999_global\\Reverse1999en',
  'Games\\reverse1999_global\\Reverse1999en',
];

const GAME_EXE = 'reverse1999.exe';

type GameSource = 'saved' | 'env' | 'registry' | 'steam' | 'common' | 'manual';

interface GameCandidate {
  path: string;
  source: GameSource;
  version?: string;
}

interface LauncherConfig {
  gamePath?: string;
}

function getConfigFile(): string {
  return path.join(app.getPath('userData'), 'launcher-config.json');
}

function loadConfig(): LauncherConfig {
  try {
    return JSON.parse(fs.readFileSync(getConfigFile(), 'utf-8')) as LauncherConfig;
  } catch {
    return {};
  }
}

function saveConfig(patch: Partial<LauncherConfig>): void {
  try {
    const next = { ...loadConfig(), ...patch };
    fs.mkdirSync(path.dirname(getConfigFile()), { recursive: true });
    fs.writeFileSync(getConfigFile(), JSON.stringify(next, null, 2), 'utf-8');
  } catch (err) {
    console.error('[config] save failed:', err);
  }
}

function hasGameExe(dir: string): boolean {
  try {
    return fs.statSync(path.join(dir, GAME_EXE)).isFile();
  } catch {
    return false;
  }
}

/**
 * Returns the folder containing reverse1999.exe: `dir` itself or a sub-folder up to `depth` levels down
 * (so picking the parent "reverse1999_global" folder still works).
 */
function findGameExeDir(dir: string, depth = 2): string | null {
  if (!dir) return null;
  if (hasGameExe(dir)) return path.normalize(dir);
  if (depth <= 0) return null;
  let entries: fs.Dirent[];
  try {
    entries = fs.readdirSync(dir, { withFileTypes: true });
  } catch {
    return null;
  }
  for (const e of entries) {
    if (!e.isDirectory()) continue;
    // Skip folders that never contain the game exe (keeps the scan fast).
    if (/^(reverse1999_Data|MonoBleedingEdge|\$Recycle\.Bin|Windows|node_modules)$/i.test(e.name)) continue;
    const found = findGameExeDir(path.join(dir, e.name), depth - 1);
    if (found) return found;
  }
  return null;
}

function readGameVersion(gameDir: string): string | undefined {
  try {
    return fs.readFileSync(path.join(getPersistentRoot(gameDir), '2585de5ae8274f5d0061316a8569a344'), 'utf-8').trim();
  } catch {
    return undefined;
  }
}

function regQuery(args: string[]): Promise<string> {
  return new Promise((resolve) => {
    execFile('reg', ['query', ...args], { windowsHide: true, maxBuffer: 32 * 1024 * 1024 }, (_err, stdout) =>
      resolve(stdout || ''),
    );
  });
}

/** Strip quotes, ",0" icon index and trailing CLI args from a registry path value. */
function cleanRegistryPath(value: string): string {
  let v = value.trim();
  if (v.startsWith('"')) {
    const end = v.indexOf('"', 1);
    v = end > 0 ? v.slice(1, end) : v.slice(1);
  } else {
    const exeIdx = v.toLowerCase().indexOf('.exe');
    if (exeIdx > 0) v = v.slice(0, exeIdx + 4);
  }
  return v.replace(/,\s*-?\d+$/, '').trim();
}

async function findFromRegistry(): Promise<string[]> {
  if (process.platform !== 'win32') return [];
  const roots = [
    'HKLM\\SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall',
    'HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall',
    'HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall',
  ];
  const dirs: string[] = [];
  for (const root of roots) {
    const out = await regQuery([root, '/s']);
    // Output = blocks separated by blank lines: "HKEY_...\Key" then "    Name    REG_SZ    Value" lines.
    for (const block of out.split(/\r?\n\r?\n/)) {
      const values: Record<string, string> = {};
      for (const line of block.split(/\r?\n/)) {
        const m = line.match(/^\s+(\S[^\t]*?)\s{2,}REG_(?:EXPAND_)?SZ\s{2,}(.*)$/);
        if (m) values[m[1].trim()] = m[2].trim();
      }
      const name = values.DisplayName || '';
      if (!/reverse\s*:?\s*1999/i.test(name)) continue;
      if (/launcher|vi[eệ]t\s*h[oó]a/i.test(name)) continue; // skip this launcher's own entry
      if (values.InstallLocation) dirs.push(cleanRegistryPath(values.InstallLocation));
      for (const key of ['DisplayIcon', 'UninstallString']) {
        if (values[key]) dirs.push(path.dirname(cleanRegistryPath(values[key])));
      }
    }
  }
  return dirs.filter(Boolean);
}

async function findFromSteam(): Promise<string[]> {
  if (process.platform !== 'win32') return [];
  const out = await regQuery(['HKCU\\Software\\Valve\\Steam', '/v', 'SteamPath']);
  const m = out.match(/SteamPath\s+REG_SZ\s+(.+)/);
  if (!m) return [];
  const steamRoot = path.normalize(m[1].trim());
  const libraries = new Set<string>([steamRoot]);
  try {
    const vdf = fs.readFileSync(path.join(steamRoot, 'steamapps', 'libraryfolders.vdf'), 'utf-8');
    for (const lm of vdf.matchAll(/"path"\s+"([^"]+)"/g)) libraries.add(path.normalize(lm[1].replace(/\\\\/g, '\\')));
  } catch {
    // no library file
  }
  const dirs: string[] = [];
  for (const lib of libraries) {
    const common = path.join(lib, 'steamapps', 'common');
    let names: string[] = [];
    try {
      names = fs.readdirSync(common);
    } catch {
      continue;
    }
    for (const n of names) if (/1999/.test(n)) dirs.push(path.join(common, n));
  }
  return dirs;
}

function findFromCommonPaths(): string[] {
  const dirs: string[] = [];
  for (let c = 67; c <= 90; c++) {
    const drive = `${String.fromCharCode(c)}:\\`;
    if (!fs.existsSync(drive)) continue;
    for (const rel of COMMON_RELATIVE_PATHS) dirs.push(path.join(drive, rel));
  }
  return dirs;
}

/** Discover every valid Reverse: 1999 install on this machine (deduplicated, in priority order). */
async function discoverGameInstallations(): Promise<GameCandidate[]> {
  const raw: Array<{ dir: string; source: GameSource }> = [];
  const saved = loadConfig().gamePath;
  if (saved) raw.push({ dir: saved, source: 'saved' });
  if (process.env.R1999_GAME_PATH) raw.push({ dir: process.env.R1999_GAME_PATH, source: 'env' });

  const [reg, steam] = await Promise.all([
    findFromRegistry().catch(() => []),
    findFromSteam().catch(() => []),
  ]);
  reg.forEach((dir) => raw.push({ dir, source: 'registry' }));
  steam.forEach((dir) => raw.push({ dir, source: 'steam' }));
  findFromCommonPaths().forEach((dir) => raw.push({ dir, source: 'common' }));

  const seen = new Set<string>();
  const result: GameCandidate[] = [];
  for (const { dir, source } of raw) {
    const exeDir = findGameExeDir(dir, source === 'common' ? 0 : 2);
    if (!exeDir) continue;
    const key = exeDir.toLowerCase();
    if (seen.has(key)) continue;
    seen.add(key);
    result.push({ path: exeDir, source, version: readGameVersion(exeDir) });
  }
  return result;
}

/**
 * Real write test: on Windows fs.access(W_OK) ignores ACLs, so "Program Files" would look writable.
 */
function isDirWritable(dir: string): boolean {
  if (!dir) return false;
  const probe = path.join(dir, `.r1999vh_write_test_${process.pid}`);
  try {
    fs.writeFileSync(probe, '');
    fs.unlinkSync(probe);
    return true;
  } catch {
    return false;
  }
}

function checkGameWritable(gameDir: string): boolean {
  if (!gameDir) return false;
  const pRoot = getPersistentRoot(gameDir);
  if (fs.existsSync(pRoot)) return isDirWritable(pRoot);
  const assets = path.join(gameDir, 'reverse1999_Data', 'StreamingAssets');
  if (fs.existsSync(assets)) return isDirWritable(assets);
  const data = path.join(gameDir, 'reverse1999_Data');
  if (fs.existsSync(data)) return isDirWritable(data);
  return isDirWritable(gameDir);
}

/**
 * Folder holding the prebuilt mod files (6ac5...dat, 7335...dat).
 * - Packaged app: <resources>/mod (shipped via electron-builder extraResources)
 * - Dev mode: parent folder of the launcher project (repository root)
 * Override with env R1999_MOD_DIR.
 */
function getModSourceDir(): string {
  if (process.env.R1999_MOD_DIR && fs.existsSync(process.env.R1999_MOD_DIR)) {
    return process.env.R1999_MOD_DIR;
  }
  const candidates = [
    path.join(process.resourcesPath, 'mod'),
    path.join(app.getAppPath(), '..'),
    path.resolve(app.getAppPath(), '..'),
    path.join(path.dirname(app.getPath('exe')), 'resources', 'mod'),
    path.join(path.dirname(app.getPath('exe'))),
    path.join(path.dirname(app.getPath('exe')), '..'),
  ];
  for (const candidate of candidates) {
    if (
      fs.existsSync(path.join(candidate, MOD_CONTAINER_NAME)) &&
      fs.existsSync(path.join(candidate, MOD_BUNDLE_NAME))
    ) {
      return candidate;
    }
  }
  if (app.isPackaged) return path.join(process.resourcesPath, 'mod');
  return path.resolve(app.getAppPath(), '..');
}

function getBackupDir(): string {
  return path.join(app.getPath('userData'), 'vanilla_backup');
}

function getPersistentRoot(gameDir: string): string {
  return path.join(gameDir, 'reverse1999_Data', 'StreamingAssets', 'PersistentRoot');
}

function fileSize(p: string): number {
  try {
    return fs.statSync(p).size;
  } catch {
    return -1;
  }
}

function md5File(p: string): string | undefined {
  try {
    return crypto.createHash('md5').update(fs.readFileSync(p)).digest('hex');
  } catch {
    return undefined;
  }
}

function friendlyFsError(err: any): string {
  if (err?.code === 'EPERM' || err?.code === 'EACCES') {
    return 'Không có quyền ghi vào thư mục game (thường do game nằm trong "Program Files"). Hãy chạy Launcher bằng quyền Administrator.';
  }
  if (err?.code === 'EBUSY') {
    return 'Tệp đang bị khóa. Hãy tắt game (reverse1999.exe) rồi thử lại.';
  }
  return err?.message || String(err);
}

let configuredGamePath = '';
let gamePathSource: GameSource | '' = '';
let initPromise: Promise<void> | null = null;

/** Resolve the game folder once at startup: saved config first, then auto-discovery. */
function ensureInit(): Promise<void> {
  if (!initPromise) {
    initPromise = (async () => {
      const saved = loadConfig().gamePath;
      const savedDir = saved ? findGameExeDir(saved, 0) : null;
      if (savedDir) {
        configuredGamePath = savedDir;
        gamePathSource = 'saved';
        return;
      }
      const found = await discoverGameInstallations();
      if (found.length > 0) {
        configuredGamePath = found[0].path;
        gamePathSource = found[0].source;
        saveConfig({ gamePath: configuredGamePath });
      }
    })();
  }
  return initPromise;
}

interface GameDetectionResult {
  detected: boolean;
  gamePath: string;
  gamePathSource?: GameSource | '';
  exePath: string;
  gameVersion: string;
  isVietnameseInstalled: boolean;
  containerMD5?: string;
  bundleSize?: number;
  modSourceDir?: string;
  modSourceAvailable?: boolean;
  hasVanillaBackup?: boolean;
  /** false => installing the mod needs the launcher to run as Administrator. */
  persistentRootWritable?: boolean;
}

function detectGameInstallation(targetDir: string = configuredGamePath): GameDetectionResult {
  const exePath = targetDir ? path.join(targetDir, 'reverse1999.exe') : '';
  const detected = !!targetDir && fs.existsSync(exePath);

  let gameVersion = '111.0.1076.0';
  const pRoot = getPersistentRoot(targetDir);
  const verHashFile = path.join(pRoot, '2585de5ae8274f5d0061316a8569a344');
  if (fs.existsSync(verHashFile)) {
    try {
      gameVersion = fs.readFileSync(verHashFile, 'utf-8').trim();
    } catch {
      // fallback
    }
  }

  const containerPath = path.join(pRoot, 'luabytes', MOD_CONTAINER_NAME);
  const bundlePath = path.join(pRoot, 'bundles', MOD_BUNDLE_NAME);

  const modDir = getModSourceDir();
  const srcContainer = path.join(modDir, MOD_CONTAINER_NAME);
  const srcBundle = path.join(modDir, MOD_BUNDLE_NAME);
  const modSourceAvailable = fs.existsSync(srcContainer) && fs.existsSync(srcBundle);

  // Vanilla files share the same names, so "exists" is not enough:
  // installed == game files match the shipped mod files (size + MD5 of the small container).
  const bundleSize = Math.max(0, fileSize(bundlePath));
  const containerMD5 = fs.existsSync(containerPath) ? md5File(containerPath) : undefined;
  const isVietnameseInstalled =
    modSourceAvailable &&
    bundleSize === fileSize(srcBundle) &&
    fileSize(containerPath) === fileSize(srcContainer) &&
    containerMD5 === md5File(srcContainer);

  const backupDir = getBackupDir();
  const bak6ac5 = path.join(backupDir, MOD_CONTAINER_NAME);
  const bak7335 = path.join(backupDir, MOD_BUNDLE_NAME);
  const hasVanillaBackup = fs.existsSync(bak6ac5) && fs.existsSync(bak7335);
  const backupSizeBytes = hasVanillaBackup ? Math.max(0, fileSize(bak6ac5)) + Math.max(0, fileSize(bak7335)) : 0;

  return {
    detected,
    gamePath: targetDir,
    gamePathSource,
    exePath,
    gameVersion,
    isVietnameseInstalled,
    containerMD5,
    bundleSize,
    modSourceDir: modDir,
    modSourceAvailable,
    hasVanillaBackup,
    backupSizeBytes,
    persistentRootWritable: detected ? checkGameWritable(targetDir) : undefined,
  };
}

const createWindow = () => {
  const window = new BrowserWindow({
    width: 1280,
    height: 740,
    minWidth: 1280,
    maxWidth: 1280,
    minHeight: 740,
    maxHeight: 740,
    resizable: false,
    maximizable: false,
    minimizable: true,
    fullscreenable: false,
    frame: false,
    show: false,
    backgroundColor: '#0d1117',
    autoHideMenuBar: true,
    title: 'Reverse: 1999 Việt Hóa Launcher',
    webPreferences: {
      preload: path.join(__dirname, 'preload.cjs'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: false,
    },
  });

  window.once('ready-to-show', () => window.show());
  if (process.env.VITE_DEV_SERVER_URL) {
    window.loadURL(process.env.VITE_DEV_SERVER_URL);
  } else {
    window.loadFile(path.join(__dirname, '../dist/index.html'));
  }
};

// Window Controls
ipcMain.on('launcher:minimize', (event) => {
  BrowserWindow.fromWebContents(event.sender)?.minimize();
});
ipcMain.on('launcher:close', (event) => {
  BrowserWindow.fromWebContents(event.sender)?.close();
});
ipcMain.handle('launcher:open-repository', () => shell.openExternal(REPOSITORY_URL));

// Game Detection & Management
ipcMain.handle('launcher:detect-game', async () => {
  await ensureInit();
  return detectGameInstallation(configuredGamePath);
});

function applyGamePath(dir: string, source: GameSource): void {
  configuredGamePath = dir;
  gamePathSource = source;
  saveConfig({ gamePath: dir });
}

ipcMain.handle('launcher:select-game-folder', async (event) => {
  await ensureInit();
  const owner = BrowserWindow.fromWebContents(event.sender) ?? undefined;
  const options: Electron.OpenDialogOptions = {
    title: 'Chọn thư mục cài đặt Reverse: 1999 (thư mục chứa reverse1999.exe)',
    properties: ['openDirectory'],
    defaultPath: configuredGamePath || undefined,
  };
  const result = owner ? await dialog.showOpenDialog(owner, options) : await dialog.showOpenDialog(options);

  if (result.canceled || result.filePaths.length === 0) {
    return { ...detectGameInstallation(configuredGamePath), canceled: true };
  }

  const picked = result.filePaths[0];
  // Accept the exact folder or a parent of it (e.g. "reverse1999_global").
  const exeDir = findGameExeDir(picked, 2);
  if (!exeDir) {
    // Keep the previous (valid) path; just report the bad selection.
    return { ...detectGameInstallation(configuredGamePath), invalidSelection: picked };
  }
  applyGamePath(exeDir, 'manual');
  return detectGameInstallation(configuredGamePath);
});

ipcMain.handle('launcher:scan-game-installations', async () => {
  await ensureInit();
  const candidates = await discoverGameInstallations();
  // Current path no longer valid (game moved/uninstalled) -> switch to the first one found.
  if (!hasGameExe(configuredGamePath) && candidates.length > 0) {
    applyGamePath(candidates[0].path, candidates[0].source);
  }
  return { candidates, current: detectGameInstallation(configuredGamePath) };
});

ipcMain.handle('launcher:set-game-path', async (_event, dir: string) => {
  await ensureInit();
  const exeDir = typeof dir === 'string' ? findGameExeDir(dir, 2) : null;
  if (!exeDir) {
    return { ...detectGameInstallation(configuredGamePath), invalidSelection: String(dir) };
  }
  applyGamePath(exeDir, 'manual');
  return detectGameInstallation(configuredGamePath);
});

ipcMain.handle('launcher:open-game-folder', async () => {
  await ensureInit();
  if (fs.existsSync(configuredGamePath)) {
    await shell.openPath(configuredGamePath);
    return { success: true };
  }
  return { success: false, error: 'Thư mục game không tồn tại' };
});

ipcMain.handle('launcher:open-persistent-root', async () => {
  await ensureInit();
  const pRoot = path.join(configuredGamePath, 'reverse1999_Data', 'StreamingAssets', 'PersistentRoot');
  if (fs.existsSync(pRoot)) {
    await shell.openPath(pRoot);
    return { success: true };
  }
  return { success: false, error: 'Thư mục PersistentRoot không tồn tại' };
});

ipcMain.handle('launcher:launch-game', async () => {
  await ensureInit();
  const exePath = path.join(configuredGamePath, 'reverse1999.exe');
  if (!fs.existsSync(exePath)) {
    return { success: false, error: 'Không tìm thấy reverse1999.exe trong thư mục đã chọn.' };
  }

  return new Promise((resolve) => {
    // reverse1999.exe yêu cầu quyền Administrator (manifest requireAdministrator).
    // Node.js spawn() gọi trực tiếp CreateProcessW và sẽ văng lỗi ERROR_ELEVATION_REQUIRED (740) -> EACCES.
    // Sử dụng PowerShell Start-Process (ShellExecuteEx) để Windows kích hoạt UAC prompt cho game một cách tự nhiên.
    const psCmd = `try { Start-Process -FilePath '${exePath.replace(/'/g, "''")}' -WorkingDirectory '${configuredGamePath.replace(/'/g, "''")}'; exit 0 } catch { exit 1223 }`;
    execFile(
      'powershell.exe',
      ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', psCmd],
      { windowsHide: true },
      (err) => {
        if (err) {
          if ((err as any).code === 1223 || String(err).includes('1223')) {
            resolve({
              success: false,
              error: 'Bạn đã từ chối cấp quyền Administrator trong bảng xác nhận UAC khi khởi chạy game.',
            });
            return;
          }
          // Fallback sang cmd /c start
          exec(`start "" /d "${configuredGamePath}" "${exePath}"`, (cmdErr) => {
            if (cmdErr) {
              resolve({
                success: false,
                error: `Không thể khởi chạy game (${cmdErr.message}). Hãy cấp quyền khi bảng UAC xuất hiện.`,
              });
            } else {
              resolve({ success: true });
            }
          });
        } else {
          resolve({ success: true });
        }
      }
    );
  });
});

// Bluepoch Hotupdate API Integration (hotupdate-hw.sl916.com)
ipcMain.handle('launcher:check-bluepoch-update', async (_event, versionOverride?: string) => {
  await ensureInit();
  const detection = detectGameInstallation(configuredGamePath);
  const version = versionOverride || detection.gameVersion || '111.0.1076.0';

  const gameId = '60001';
  const osType = '2';
  const subChannel = '4';
  const versionType = '200';
  const channelId = '6001';

  const rawSign = `${gameId}_${osType}_${subChannel}_${versionType}_${channelId}_${version}`;
  const sign = crypto.createHash('md5').update(rawSign, 'utf8').digest('hex');

  const queryParams = new URLSearchParams({
    gameId,
    osType,
    subChannel,
    versionType,
    channelId,
    version,
    sign,
    localhash: '2585de5ae8274f5d0061316a8569a344',
  });

  const endpoints = {
    diffVersion: `https://hotupdate-hw.sl916.com/diff-update/version?${queryParams.toString()}`,
    patchVersion: `https://hotupdate-hw.sl916.com/patch/60001/version?${queryParams.toString()}`,
  };

  const results: Record<string, any> = {};

  try {
    const resp = await fetch(endpoints.patchVersion, {
      headers: { 'User-Agent': `Reverse1999/${version}`, Accept: 'application/json' },
      signal: AbortSignal.timeout(8000),
    });
    results.patchVersion = await resp.json();
  } catch (err: any) {
    results.patchVersion = { error: err.name === 'TimeoutError' ? 'Hết thời gian chờ phản hồi (8s)' : err.message };
  }

  try {
    const resp = await fetch(endpoints.diffVersion, {
      headers: { 'User-Agent': `Reverse1999/${version}`, Accept: 'application/json' },
      signal: AbortSignal.timeout(8000),
    });
    results.diffVersion = await resp.json();
  } catch (err: any) {
    results.diffVersion = { error: err.name === 'TimeoutError' ? 'Hết thời gian chờ phản hồi (8s)' : err.message };
  }

  return {
    success: true,
    localVersion: version,
    signature: sign,
    rawSignatureString: rawSign,
    checkedAt: new Date().toISOString(),
    endpoints,
    results,
  };
});

ipcMain.handle('launcher:check-bluepoch-resources', async (_event, versionOverride?: string) => {
  await ensureInit();
  const detection = detectGameInstallation(configuredGamePath);
  const version = versionOverride || detection.gameVersion || '111.0.1076.0';

  const gameId = '60001';
  const osType = '2';
  const subChannel = '4';
  const versionType = '200';
  const channelId = '6001';

  const rawSign = `${gameId}_${osType}_${subChannel}_${versionType}_${channelId}_${version}`;
  const sign = crypto.createHash('md5').update(rawSign, 'utf8').digest('hex');

  const queryParams = new URLSearchParams({
    gameId,
    osType,
    subChannel,
    versionType,
    channelId,
    version,
    sign,
    localhash: '2585de5ae8274f5d0061316a8569a344',
  });

  const url = `https://hotupdate-hw.sl916.com/diff-update/resource?${queryParams.toString()}`;

  try {
    const resp = await fetch(url, {
      headers: { 'User-Agent': `Reverse1999/${version}`, Accept: 'application/json' },
      signal: AbortSignal.timeout(10000),
    });
    const data = await resp.json();
    return {
      success: true,
      url,
      version,
      sign,
      manifest: data,
    };
  } catch (err: any) {
    return {
      success: false,
      url,
      error: err.name === 'TimeoutError' ? 'Hết thời gian chờ phản hồi từ máy chủ (10s)' : err.message,
    };
  }
});

function runElevatedScript(scriptLines: string[]): Promise<{ success: boolean; error?: string }> {
  return new Promise((resolve) => {
    const tmpScriptPath = path.join(app.getPath('temp'), `r1999_elevated_${Date.now()}.ps1`);
    const statusFile = path.join(app.getPath('temp'), `r1999_status_${Date.now()}.txt`);

    const fullScript = [
      '$ErrorActionPreference = "Stop"',
      'try {',
      ...scriptLines.map((l) => '  ' + l),
      `  [IO.File]::WriteAllText('${statusFile.replace(/'/g, "''")}', 'SUCCESS')`,
      '} catch {',
      `  [IO.File]::WriteAllText('${statusFile.replace(/'/g, "''")}', 'ERROR:' + $_.Exception.Message)`,
      '}',
    ].join('\r\n');

    fs.writeFileSync(tmpScriptPath, fullScript, { encoding: 'utf8' });

    // Launch PowerShell with -Verb RunAs so Windows triggers UAC
    const psCommand = `try { $p = Start-Process powershell -Verb RunAs -WindowStyle Hidden -Wait -PassThru -ArgumentList '-NoProfile -ExecutionPolicy Bypass -File \"${tmpScriptPath.replace(/"/g, '`"')}\"'; exit $p.ExitCode } catch { exit 1223 }`;

    execFile(
      'powershell.exe',
      ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', psCommand],
      { windowsHide: true },
      (err) => {
        let result: { success: boolean; error?: string } = { success: false, error: 'Thao tác không hoàn thành' };

        if (fs.existsSync(statusFile)) {
          try {
            const content = fs.readFileSync(statusFile, 'utf-8').trim();
            if (content === 'SUCCESS') {
              result = { success: true };
            } else if (content.startsWith('ERROR:')) {
              result = { success: false, error: content.replace('ERROR:', '').trim() };
            }
            fs.unlinkSync(statusFile);
          } catch {
            // ignore
          }
        } else if (err && ((err as any).code === 1223 || String(err).includes('1223'))) {
          result = {
            success: false,
            error: 'Bạn đã từ chối cấp quyền Administrator trong bảng xác nhận UAC của Windows.',
          };
        } else {
          result = {
            success: false,
            error: 'Không thể nâng quyền thực thi (người dùng đã hủy UAC hoặc bị chính sách chặn).',
          };
        }

        try {
          if (fs.existsSync(tmpScriptPath)) fs.unlinkSync(tmpScriptPath);
        } catch {
          // ignore
        }

        resolve(result);
      }
    );
  });
}

// Localization Mod Deployment & Rollback (with Just-In-Time UAC Elevation)
ipcMain.handle('launcher:install-vietnamese', async () => {
  await ensureInit();
  const detection = detectGameInstallation(configuredGamePath);
  if (!detection.detected) {
    return { success: false, error: 'Không tìm thấy thư mục cài đặt game hợp lệ.' };
  }

  const pRoot = getPersistentRoot(configuredGamePath);
  const targetLuabytes = path.join(pRoot, 'luabytes');
  const targetBundles = path.join(pRoot, 'bundles');

  const modDir = getModSourceDir();
  const src6ac5 = path.join(modDir, MOD_CONTAINER_NAME);
  const src7335 = path.join(modDir, MOD_BUNDLE_NAME);

  if (!fs.existsSync(src6ac5) || !fs.existsSync(src7335)) {
    return {
      success: false,
      error: `Không tìm thấy tệp bản dịch nguồn trong: ${modDir} (cần ${MOD_CONTAINER_NAME} và ${MOD_BUNDLE_NAME}).`,
    };
  }

  const dest6ac5 = path.join(targetLuabytes, MOD_CONTAINER_NAME);
  const dest7335 = path.join(targetBundles, MOD_BUNDLE_NAME);
  const backupDir = getBackupDir();

  // 1. Thử copy trực tiếp nếu thư mục đã có quyền ghi
  if (isDirWritable(pRoot)) {
    try {
      fs.mkdirSync(targetLuabytes, { recursive: true });
      fs.mkdirSync(targetBundles, { recursive: true });

      if (!detection.isVietnameseInstalled) {
        fs.mkdirSync(backupDir, { recursive: true });
        if (fs.existsSync(dest6ac5)) fs.copyFileSync(dest6ac5, path.join(backupDir, MOD_CONTAINER_NAME));
        if (fs.existsSync(dest7335)) fs.copyFileSync(dest7335, path.join(backupDir, MOD_BUNDLE_NAME));
      }

      fs.copyFileSync(src6ac5, dest6ac5);
      fs.copyFileSync(src7335, dest7335);

      return { success: true, message: 'Đã cài đặt bản Việt Hóa v0.1.18.2 Full vào PersistentRoot thành công!' };
    } catch (err: any) {
      if (err.code !== 'EPERM' && err.code !== 'EACCES') {
        return { success: false, error: friendlyFsError(err) };
      }
      // Bị lỗi quyền ghi -> Chuyển sang kích hoạt UAC bên dưới
    }
  }

  // 2. Kích hoạt quyền Administrator tức thời qua Windows UAC (Phương án 1)
  const scriptLines: string[] = [
    `New-Item -ItemType Directory -Force -Path '${targetLuabytes.replace(/'/g, "''")}' | Out-Null`,
    `New-Item -ItemType Directory -Force -Path '${targetBundles.replace(/'/g, "''")}' | Out-Null`,
  ];

  if (!detection.isVietnameseInstalled) {
    scriptLines.push(
      `New-Item -ItemType Directory -Force -Path '${backupDir.replace(/'/g, "''")}' | Out-Null`,
      `if (Test-Path '${dest6ac5.replace(/'/g, "''")}') { Copy-Item -Force '${dest6ac5.replace(/'/g, "''")}' '${path.join(backupDir, MOD_CONTAINER_NAME).replace(/'/g, "''")}' }`,
      `if (Test-Path '${dest7335.replace(/'/g, "''")}') { Copy-Item -Force '${dest7335.replace(/'/g, "''")}' '${path.join(backupDir, MOD_BUNDLE_NAME).replace(/'/g, "''")}' }`
    );
  }

  scriptLines.push(
    `Copy-Item -Force '${src6ac5.replace(/'/g, "''")}' '${dest6ac5.replace(/'/g, "''")}'`,
    `Copy-Item -Force '${src7335.replace(/'/g, "''")}' '${dest7335.replace(/'/g, "''")}'`
  );

  const elevatedRes = await runElevatedScript(scriptLines);
  if (elevatedRes.success) {
    return {
      success: true,
      message: 'Đã cấp quyền Administrator qua UAC và cài đặt bản Việt Hóa v0.1.18.2 Full thành công!',
    };
  } else {
    return { success: false, error: elevatedRes.error };
  }
});

ipcMain.handle('launcher:uninstall-vietnamese', async () => {
  await ensureInit();
  const pRoot = getPersistentRoot(configuredGamePath);
  const dest6ac5 = path.join(pRoot, 'luabytes', MOD_CONTAINER_NAME);
  const dest7335 = path.join(pRoot, 'bundles', MOD_BUNDLE_NAME);
  const backupDir = getBackupDir();
  const bak6ac5 = path.join(backupDir, MOD_CONTAINER_NAME);
  const bak7335 = path.join(backupDir, MOD_BUNDLE_NAME);

  if (isDirWritable(pRoot)) {
    try {
      if (fs.existsSync(bak6ac5) && fs.existsSync(bak7335)) {
        fs.copyFileSync(bak6ac5, dest6ac5);
        fs.copyFileSync(bak7335, dest7335);
        return { success: true, message: 'Đã khôi phục tệp gốc từ bản sao lưu. Game sẽ chạy bằng ngôn ngữ gốc.' };
      }

      if (fs.existsSync(dest6ac5)) fs.unlinkSync(dest6ac5);
      if (fs.existsSync(dest7335)) fs.unlinkSync(dest7335);
      return { success: true, message: 'Đã gỡ bỏ bản Việt hóa. Game sẽ chạy bằng ngôn ngữ gốc.' };
    } catch (err: any) {
      if (err.code !== 'EPERM' && err.code !== 'EACCES') {
        return { success: false, error: friendlyFsError(err) };
      }
    }
  }

  // Nâng quyền UAC để gỡ bỏ / khôi phục
  const scriptLines: string[] = [];
  if (fs.existsSync(bak6ac5) && fs.existsSync(bak7335)) {
    scriptLines.push(
      `Copy-Item -Force '${bak6ac5.replace(/'/g, "''")}' '${dest6ac5.replace(/'/g, "''")}'`,
      `Copy-Item -Force '${bak7335.replace(/'/g, "''")}' '${dest7335.replace(/'/g, "''")}'`
    );
  } else {
    scriptLines.push(
      `if (Test-Path '${dest6ac5.replace(/'/g, "''")}') { Remove-Item -Force '${dest6ac5.replace(/'/g, "''")}' }`,
      `if (Test-Path '${dest7335.replace(/'/g, "''")}') { Remove-Item -Force '${dest7335.replace(/'/g, "''")}' }`
    );
  }

  const elevatedRes = await runElevatedScript(scriptLines);
  if (elevatedRes.success) {
    return {
      success: true,
      message: 'Đã cấp quyền Administrator qua UAC và khôi phục ngôn ngữ gốc thành công!',
    };
  } else {
    return { success: false, error: elevatedRes.error };
  }
});

// Step 4: Integrity Verification & Diagnostics
ipcMain.handle('launcher:verify-integrity', async () => {
  await ensureInit();
  const detection = detectGameInstallation(configuredGamePath);
  if (!detection.detected) {
    return {
      status: 'missing',
      summary: 'Không tìm thấy thư mục cài đặt game để kiểm tra.',
      details: null,
    };
  }

  const pRoot = getPersistentRoot(configuredGamePath);
  const inst6ac5 = path.join(pRoot, 'luabytes', MOD_CONTAINER_NAME);
  const inst7335 = path.join(pRoot, 'bundles', MOD_BUNDLE_NAME);

  const modDir = getModSourceDir();
  const src6ac5 = path.join(modDir, MOD_CONTAINER_NAME);
  const src7335 = path.join(modDir, MOD_BUNDLE_NAME);

  const hasInst6ac5 = fs.existsSync(inst6ac5);
  const hasInst7335 = fs.existsSync(inst7335);

  const inst6ac5MD5 = hasInst6ac5 ? md5File(inst6ac5) : null;
  const src6ac5MD5 = fs.existsSync(src6ac5) ? md5File(src6ac5) : null;
  const inst7335Size = hasInst7335 ? fileSize(inst7335) : 0;
  const src7335Size = fs.existsSync(src7335) ? fileSize(src7335) : 0;

  const containerMatches = !!(inst6ac5MD5 && src6ac5MD5 && inst6ac5MD5.toLowerCase() === src6ac5MD5.toLowerCase());
  const bundleMatches = inst7335Size > 0 && src7335Size > 0 && Math.abs(inst7335Size - src7335Size) < 1024;

  let status: 'intact' | 'modified' | 'vanilla' | 'missing' = 'missing';
  let summary = '';

  if (containerMatches && bundleMatches) {
    status = 'intact';
    summary = 'Tất cả các tệp bản dịch (Container Lua & AssetBundle 7335) hoàn toàn nguyên vẹn và khớp 100% với bản Route B v5 Clean chuẩn.';
  } else if (!hasInst6ac5 && !hasInst7335) {
    status = 'missing';
    summary = 'Chưa tìm thấy tệp mod trong thư mục game.';
  } else if (!containerMatches && !bundleMatches) {
    status = 'vanilla';
    summary = 'Game đang ở trạng thái nguyên bản gốc (chưa nạp container hoặc bundle tiếng Việt).';
  } else {
    status = 'modified';
    summary = 'Phát hiện sự sai lệch giữa tệp trong game và tệp bản dịch chuẩn (có thể do game vừa cập nhật tài nguyên). Khuyến nghị cài đặt/đồng bộ lại.';
  }

  return {
    status,
    summary,
    details: {
      gameVersion: detection.gameVersion,
      containerMatches,
      installedContainerMD5: inst6ac5MD5,
      expectedContainerMD5: src6ac5MD5,
      bundleMatches,
      installedBundleSize: inst7335Size,
      expectedBundleSize: src7335Size,
      checkedAt: new Date().toISOString(),
    },
  };
});

// Step 4: Open Backup Folder
ipcMain.handle('launcher:open-backup-folder', async () => {
  const bDir = getBackupDir();
  fs.mkdirSync(bDir, { recursive: true });
  await shell.openPath(bDir);
  return { success: true };
});

// Step 4: GitHub Community Update Check
ipcMain.handle('launcher:check-github-update', async () => {
  const currentLauncherVersion = '1.0.0';
  const currentModVersion = '0.1.18.2';
  try {
    const res = await fetch('https://api.github.com/repos/lquan-tech/viLauncher/releases/latest', {
      headers: { 'User-Agent': 'Reverse1999-Launcher', Accept: 'application/json' },
      signal: AbortSignal.timeout(6000),
    });
    if (res.status === 200) {
      const data = await res.json();
      return {
        success: true,
        hasUpdate: data.tag_name !== currentLauncherVersion && data.tag_name !== currentModVersion,
        latestTag: data.tag_name,
        releaseName: data.name || data.tag_name,
        releaseNotes: data.body || '',
        releaseUrl: data.html_url || REPOSITORY_URL,
        currentLauncherVersion,
        currentModVersion,
      };
    }
  } catch {
    // Network or timeout fallback
  }

  return {
    success: true,
    hasUpdate: false,
    latestTag: 'v0.1.18.2',
    releaseName: 'Bản Việt Hóa v0.1.18.2 Full Clean',
    releaseNotes: 'Bạn đang sử dụng phiên bản mới nhất từ kho lưu trữ cộng đồng.',
    releaseUrl: REPOSITORY_URL,
    currentLauncherVersion,
    currentModVersion,
  };
});

app.whenReady().then(() => {
  void ensureInit();
  createWindow();
  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) createWindow();
  });
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') app.quit();
});
