type GameSource = 'saved' | 'env' | 'registry' | 'steam' | 'common' | 'manual';

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
  backupSizeBytes?: number;
  /** false => installing the mod requires running the launcher as Administrator. */
  persistentRootWritable?: boolean;
}

interface IntegrityCheckResult {
  status: 'intact' | 'modified' | 'vanilla' | 'missing';
  summary: string;
  details: {
    gameVersion?: string;
    containerMatches: boolean;
    installedContainerMD5: string | null;
    expectedContainerMD5: string | null;
    bundleMatches: boolean;
    installedBundleSize: number;
    expectedBundleSize: number;
    checkedAt: string;
  } | null;
}

interface GithubUpdateResult {
  success: boolean;
  hasUpdate: boolean;
  latestTag: string;
  releaseName: string;
  releaseNotes: string;
  releaseUrl: string;
  currentLauncherVersion: string;
  currentModVersion: string;
}

interface GameFolderSelectionResult extends GameDetectionResult {
  canceled?: boolean;
  /** Folder the user picked that does not contain reverse1999.exe (previous path kept). */
  invalidSelection?: string;
}

interface GameCandidate {
  path: string;
  source: GameSource;
  version?: string;
}

interface GameScanResult {
  candidates: GameCandidate[];
  current: GameDetectionResult;
}

interface BluepochUpdateResult {
  success: boolean;
  localVersion: string;
  signature: string;
  rawSignatureString: string;
  checkedAt: string;
  endpoints: {
    diffVersion: string;
    patchVersion: string;
  };
  results: {
    diffVersion?: any;
    patchVersion?: any;
  };
}

interface BluepochResourceResult {
  success: boolean;
  url: string;
  version?: string;
  sign?: string;
  manifest?: any;
  error?: string;
}

interface Window {
  launcher?: {
    minimize: () => void;
    close: () => void;
    openRepository: () => Promise<void>;
    detectGame: () => Promise<GameDetectionResult>;
    selectGameFolder: () => Promise<GameFolderSelectionResult>;
    scanGameInstallations: () => Promise<GameScanResult>;
    setGamePath: (dir: string) => Promise<GameFolderSelectionResult>;
    openGameFolder: () => Promise<{ success: boolean; error?: string }>;
    openPersistentRoot: () => Promise<{ success: boolean; error?: string }>;
    launchGame: () => Promise<{ success: boolean; error?: string }>;
    checkBluepochUpdate: (version?: string) => Promise<BluepochUpdateResult>;
    checkBluepochResources: (version?: string) => Promise<BluepochResourceResult>;
    installVietnamese: () => Promise<{ success: boolean; message?: string; error?: string }>;
    uninstallVietnamese: () => Promise<{ success: boolean; message?: string; error?: string }>;
    verifyIntegrity: () => Promise<IntegrityCheckResult>;
    openBackupFolder: () => Promise<{ success: boolean }>;
    checkGithubUpdate: () => Promise<GithubUpdateResult>;
  };
}
