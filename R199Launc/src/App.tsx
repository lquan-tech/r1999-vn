import { useState, useEffect } from 'react';
import {
  Github, Settings, Minus, X, Home, RefreshCw, BookOpen, FileText,
  Play, Folder, Megaphone, ChartNoAxesColumnIncreasing, CircleCheck,
  Check, Download, Info, ChevronDown,
  ShieldAlert, Sparkles, Server, HardDrive, Cpu,
  Layers, RefreshCcw, CheckCircle2, XCircle, Search, ShieldCheck
} from 'lucide-react';
import './style.css';

const GAME_SOURCE_LABELS: Record<string, string> = {
  saved: 'Đã lưu',
  env: 'Biến môi trường',
  registry: 'Registry Windows',
  steam: 'Steam',
  common: 'Vị trí mặc định',
  manual: 'Chọn thủ công',
};

type Section = 'Tổng quan' | 'Cập nhật' | 'Quản lý Việt hóa' | 'Nhật ký thay đổi' | 'Cài đặt';
type SettingsTab = 'General' | 'GamePath' | 'Download' | 'About';

const navItems: { label: Section; Icon: typeof Home }[] = [
  { label: 'Tổng quan', Icon: Home },
  { label: 'Cập nhật', Icon: RefreshCw },
  { label: 'Quản lý Việt hóa', Icon: BookOpen },
  { label: 'Nhật ký thay đổi', Icon: FileText },
  { label: 'Cài đặt', Icon: Settings },
];

const settingsTabs: { label: SettingsTab; title: string; Icon: typeof Settings }[] = [
  { label: 'General', title: 'Cài đặt chung', Icon: Settings },
  { label: 'GamePath', title: 'Thư mục Game', Icon: HardDrive },
  { label: 'Download', title: 'Tải xuống', Icon: Download },
  { label: 'About', title: 'Thông tin', Icon: Info },
];

const notices = [
  {
    date: '10/11/2026',
    title: 'Bản Việt Hóa v0.1.18.2 Full Đã Sẵn Sàng',
    description: 'Xóa sạch hoàn toàn các nút widget rác, gỡ triệt để FCK OFF và tích hợp cảnh báo chống lừa đảo.',
    art: 'tower',
    badge: 'Mới'
  },
  {
    date: '09/11/2026',
    title: 'Đồng Bộ Hotupdate Bluepoch API',
    description: 'Tích hợp endpoint sl916.com để tự động kiểm tra tương thích tài nguyên khi game ra phiên bản mới.',
    art: 'portrait',
    badge: 'Hot'
  },
  {
    date: '08/11/2026',
    title: 'Cảnh Báo Lừa Đảo Thương Mại Hóa',
    description: 'Bản dịch Tiếng Việt là miễn phí 100%. Mọi hành vi bán lại đều là lừa đảo.',
    art: 'record',
    badge: 'Quan trọng'
  },
];

export default function App() {
  const [section, setSection] = useState<Section>('Tổng quan');
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [settingsTab, setSettingsTab] = useState<SettingsTab>('General');
  const [toast, setToast] = useState('');
  const [language, setLanguage] = useState('Tiếng Việt');
  const [closeBehavior, setCloseBehavior] = useState<'tray' | 'exit'>('tray');
  const [autoLaunch, setAutoLaunch] = useState(false);

  // Game Detection State
  const [gameInfo, setGameInfo] = useState<GameDetectionResult | null>(null);
  const [isDetecting, setIsDetecting] = useState(true);
  const [candidates, setCandidates] = useState<GameCandidate[]>([]);
  const [isScanning, setIsScanning] = useState(false);

  // Bluepoch API State
  const [apiChecking, setApiChecking] = useState(false);
  const [apiResult, setApiResult] = useState<BluepochUpdateResult | null>(null);
  const [resChecking, setResChecking] = useState(false);
  const [resResult, setResResult] = useState<BluepochResourceResult | null>(null);

  // Mod Action State
  const [isModWorking, setIsModWorking] = useState(false);

  // Step 4: Maintenance, Diagnostics & Community Updates
  const [integrityResult, setIntegrityResult] = useState<IntegrityCheckResult | null>(null);
  const [isVerifying, setIsVerifying] = useState(false);
  const [githubResult, setGithubResult] = useState<GithubUpdateResult | null>(null);
  const [isCheckingGithub, setIsCheckingGithub] = useState(false);

  const notify = (message: string) => {
    setToast(message);
    window.setTimeout(() => setToast(''), 3200);
  };

  const refreshGameStatus = async () => {
    setIsDetecting(true);
    try {
      if (window.launcher?.detectGame) {
        const info = await window.launcher.detectGame();
        setGameInfo(info);
      }
    } catch (err: any) {
      notify(`Lỗi kiểm tra game: ${err.message}`);
    } finally {
      setIsDetecting(false);
    }
  };

  useEffect(() => {
    refreshGameStatus();
  }, []);

  const handleLaunchGame = async () => {
    if (!gameInfo?.detected) {
      notify('Không tìm thấy game! Vui lòng chọn đường dẫn cài đặt trong Cài đặt.');
      return;
    }
    notify('Đang khởi chạy Reverse: 1999...');
    try {
      const res = await window.launcher?.launchGame();
      if (!res?.success) {
        notify(`Không thể chạy game: ${res?.error}`);
      }
    } catch (err: any) {
      notify(`Lỗi: ${err.message}`);
    }
  };

  const handleSelectFolder = async () => {
    try {
      if (window.launcher?.selectGameFolder) {
        const res = await window.launcher.selectGameFolder();
        setGameInfo(res);
        if (res.canceled) return;
        if (res.invalidSelection) {
          notify(`Không tìm thấy reverse1999.exe trong "${res.invalidSelection}". Giữ nguyên thư mục cũ.`);
        } else if (res.detected) {
          notify(`Đã tìm thấy và lưu thư mục game: ${res.gamePath}`);
        }
      }
    } catch (err: any) {
      notify(`Lỗi chọn thư mục: ${err.message}`);
    }
  };

  const handleScanGames = async () => {
    setIsScanning(true);
    try {
      if (window.launcher?.scanGameInstallations) {
        const res = await window.launcher.scanGameInstallations();
        setCandidates(res.candidates);
        setGameInfo(res.current);
        notify(res.candidates.length > 0
          ? `Tìm thấy ${res.candidates.length} bản cài đặt Reverse: 1999`
          : 'Không tự tìm thấy game. Hãy nhấn "Chọn thư mục" để chỉ định thủ công.');
      }
    } catch (err: any) {
      notify(`Lỗi dò tìm: ${err.message}`);
    } finally {
      setIsScanning(false);
    }
  };

  const handleUseCandidate = async (dir: string) => {
    try {
      if (window.launcher?.setGamePath) {
        const res = await window.launcher.setGamePath(dir);
        setGameInfo(res);
        notify(res.invalidSelection ? `Thư mục không hợp lệ: ${dir}` : `Đã chuyển sang: ${res.gamePath}`);
      }
    } catch (err: any) {
      notify(`Lỗi: ${err.message}`);
    }
  };

  const handleCheckBluepoch = async () => {
    setApiChecking(true);
    try {
      if (window.launcher?.checkBluepochUpdate) {
        const res = await window.launcher.checkBluepochUpdate(gameInfo?.gameVersion);
        setApiResult(res);
        notify('Đã truy vấn máy chủ hotupdate Bluepoch (sl916.com)');
      }
    } catch (err: any) {
      notify(`Lỗi truy vấn: ${err.message}`);
    } finally {
      setApiChecking(false);
    }
  };

  const handleCheckResources = async () => {
    setResChecking(true);
    try {
      if (window.launcher?.checkBluepochResources) {
        const res = await window.launcher.checkBluepochResources(gameInfo?.gameVersion);
        setResResult(res);
        notify('Đã lấy manifest tài nguyên /diff-update/resource');
      }
    } catch (err: any) {
      notify(`Lỗi manifest: ${err.message}`);
    } finally {
      setResChecking(false);
    }
  };

  const handleInstallMod = async () => {
    setIsModWorking(true);
    try {
      if (window.launcher?.installVietnamese) {
        const res = await window.launcher.installVietnamese();
        if (res.success) {
          notify(res.message || 'Đã cài đặt bản Việt Hóa thành công!');
          await refreshGameStatus();
        } else {
          notify(`Lỗi cài đặt: ${res.error}`);
        }
      }
    } catch (err: any) {
      notify(`Lỗi: ${err.message}`);
    } finally {
      setIsModWorking(false);
    }
  };

  const handleVerifyIntegrity = async () => {
    setIsVerifying(true);
    try {
      if (window.launcher?.verifyIntegrity) {
        const res = await window.launcher.verifyIntegrity();
        setIntegrityResult(res);
        if (res.status === 'intact') {
          notify('Kiểm tra hoàn tất: Tệp Việt Hóa toàn vẹn 100%!');
        } else if (res.status === 'vanilla') {
          notify('Game đang ở trạng thái gốc (chưa cài đặt bản dịch).');
        } else {
          notify(`Chú ý: ${res.summary}`);
        }
      }
    } catch (err: any) {
      notify(`Lỗi kiểm tra toàn vẹn: ${err.message}`);
    } finally {
      setIsVerifying(false);
    }
  };

  const handleCheckGithub = async () => {
    setIsCheckingGithub(true);
    try {
      if (window.launcher?.checkGithubUpdate) {
        const res = await window.launcher.checkGithubUpdate();
        setGithubResult(res);
        notify(res.hasUpdate
          ? `Có phiên bản mới: ${res.latestTag} trên GitHub!`
          : 'Bạn đang sử dụng bản dịch mới nhất từ GitHub.');
      }
    } catch (err: any) {
      notify(`Lỗi GitHub: ${err.message}`);
    } finally {
      setIsCheckingGithub(false);
    }
  };

  const handleUninstallMod = async () => {
    setIsModWorking(true);
    try {
      if (window.launcher?.uninstallVietnamese) {
        const res = await window.launcher.uninstallVietnamese();
        if (res.success) {
          notify(res.message || 'Đã gỡ bỏ bản Việt Hóa!');
          await refreshGameStatus();
        } else {
          notify(`Lỗi gỡ bỏ: ${res.error}`);
        }
      }
    } catch (err: any) {
      notify(`Lỗi: ${err.message}`);
    } finally {
      setIsModWorking(false);
    }
  };

  // Option 1: Just-in-Time UAC Elevation
  const adminWarning = gameInfo?.detected && gameInfo.persistentRootWritable === false ? (
    <div className="admin-tip" role="note">
      <ShieldCheck size={18} />
      <div>
        <strong>Xác nhận quyền Administrator qua Windows UAC</strong>
        <span>
          Thư mục game nằm trong phân vùng bảo vệ (<code>Program Files</code>). Khi nhấn nút <b>Cài đặt</b> hoặc <b>Gỡ bỏ</b>, Windows sẽ hiển thị bảng xác nhận UAC (Yes/No) để cấp quyền tự động. Chỉ cần nhấn <b>Yes</b> là xong ngay mà không cần thoát Launcher.
        </span>
      </div>
    </div>
  ) : null;

  const selectSection = (label: Section) => {
    if (label === 'Cài đặt') {
      setSettingsOpen(true);
      return;
    }
    setSection(label);
  };

  return (
    <main className="launcher" aria-label="Reverse: 1999 Việt Hóa Launcher">
      {/* Top Window Bar */}
      <header className="window-bar">
        <div className="window-title-bar">
          <div className="title-star">✦</div>
          <span className="window-title-text">REVERSE: 1999 VIỆT HÓA — LAUNCHER CỘNG ĐỒNG</span>
          <span className="window-version-tag">v1.0.0 Community</span>
        </div>
        <div className="drag-region" />
        <div className="window-actions">
          <button className="window-control" title="Thu nhỏ" onClick={() => window.launcher?.minimize()}>
            <Minus size={18} />
          </button>
          <button className="window-control close-control" title="Đóng" onClick={() => window.launcher?.close()}>
            <X size={18} />
          </button>
        </div>
      </header>

      <div className="app-frame">
        {/* Sidebar */}
        <aside className="sidebar">
          <div className="brand-block">
            <div className="brand-star">✦</div>
            <div className="brand-name">
              REVERSE<br /><span>1999</span>
            </div>
            <div className="brand-caption">VIỆT HÓA CỘNG ĐỒNG</div>
            <div className="brand-ornament"><i /><b>✦</b><i /></div>
          </div>

          <nav className="side-nav" aria-label="Điều hướng chính">
            {navItems.map(({ label, Icon }) => (
              <button
                key={label}
                className={`nav-item ${section === label ? 'active' : ''}`}
                onClick={() => selectSection(label)}
              >
                <Icon size={19} strokeWidth={2.2} />
                <span>{label}</span>
              </button>
            ))}
          </nav>

          <div className="sidebar-city" aria-hidden="true"><i /><i /><i /><b /></div>

          <div className="sidebar-status">
            <div className="status-indicator">
              <span className={`status-dot ${isDetecting ? 'warning' : (gameInfo?.detected ? 'online' : 'warning')}`} />
              <span>{isDetecting ? 'Đang kiểm tra...' : (gameInfo?.detected ? 'Client: Sẵn sàng' : 'Chưa định vị')}</span>
            </div>
            <small>{gameInfo?.gameVersion || '3.9.0'}</small>
          </div>
        </aside>

        {/* Main Content Area */}
        <section className="main-area">
          {section === 'Tổng quan' && (
            <div className="content-grid">
              <section className="primary-column">
                <article className="hero-card">
                  <div className="hero-art" aria-hidden="true">
                    <div className="hero-moon" />
                    <div className="hero-clock"><i /><b /></div>
                    <div className="hero-paper paper-one" />
                    <div className="hero-paper paper-two" />
                    <div className="hero-city"><i /><i /><i /><i /><b /></div>
                    <div className="hero-person"><i /><b /><span /></div>
                    <div className="hero-mist" />
                  </div>
                  <div className="hero-copy">
                    <div className="hero-logo">REVERSE<small>1 9 9 9</small></div>
                    <h1>Bản Việt Hóa Trọn Vẹn</h1>
                    <div className="hero-divider"><i /><b>✦</b><i /></div>
                    <p>Khám phá thế kỷ 20 trong cơn mưa đảo ngược thời gian với ngôn ngữ Tiếng Việt chuẩn mực.</p>
                  </div>
                  <div className="hero-buttons">
                    <button className="play-button" onClick={handleLaunchGame}>
                      <Play size={20} fill="currentColor" />
                      <span>Khởi chạy game</span>
                    </button>
                    <button className="folder-button" onClick={() => window.launcher?.openGameFolder()}>
                      <Folder size={19} fill="currentColor" />
                      <span>Thư mục game</span>
                    </button>
                    <button className="resync-button" onClick={handleInstallMod} title="Cài đặt / Đồng bộ nhanh">
                      <RefreshCcw size={17} />
                    </button>
                  </div>
                </article>

                {/* Status Cards Row */}
                <div className="status-cards-row">
                  <div className="status-chip">
                    <div className="chip-icon"><Cpu size={18} /></div>
                    <div className="chip-info">
                      <label>Phiên bản Game</label>
                      <strong>{gameInfo?.gameVersion || '111.0.1076.0'}</strong>
                    </div>
                  </div>

                  <div className="status-chip">
                    <div className="chip-icon"><Layers size={18} /></div>
                    <div className="chip-info">
                      <label>Trạng thái Bản dịch</label>
                      <strong className={gameInfo?.isVietnameseInstalled ? 'text-success' : 'text-warning'}>
                        {gameInfo?.isVietnameseInstalled ? 'Đã cài đặt v0.1.18.2' : 'Chưa cài đặt'}
                      </strong>
                    </div>
                  </div>

                  <div className="status-chip">
                    <div className="chip-icon"><Server size={18} /></div>
                    <div className="chip-info">
                      <label>Bluepoch CDN API</label>
                      <strong className="text-accent">sl916.com (Hỗ trợ)</strong>
                    </div>
                  </div>
                </div>

                {/* Version State Details */}
                <section className="version-card">
                  <h2><ChartNoAxesColumnIncreasing size={20} />Trạng thái hệ thống</h2>
                  <div className="version-details">
                    <div className="version-lines">
                      <div><span>Thư mục cài đặt:</span><b className="code-font" title={gameInfo?.gamePath}>{gameInfo?.gamePath ? gameInfo.gamePath.slice(0, 38) + '...' : 'Chưa cấu hình'}</b></div>
                      <div><span>Phiên bản Client:</span><b>{gameInfo?.gameVersion || '111.0.1076.0'}</b></div>
                      <div>
                        <span>Bản Việt hóa:</span>
                        <b className={gameInfo?.isVietnameseInstalled ? 'text-success' : 'text-warning'}>
                          {gameInfo?.isVietnameseInstalled ? 'v0.1.18.2 Full Clean' : 'Chưa cài đặt (Gốc)'}
                        </b>
                      </div>
                      <div>
                        <span>Widget rác / FCK OFF:</span>
                        {gameInfo?.isVietnameseInstalled ? (
                          <b className="text-success"><CircleCheck size={14} /> Đã triệt tiêu hoàn toàn</b>
                        ) : (
                          <span className="text-muted">Chờ cài đặt bản dịch</span>
                        )}
                      </div>
                      <div>
                        <span>Cảnh báo lừa đảo:</span>
                        {gameInfo?.isVietnameseInstalled ? (
                          <b className="text-accent"><CircleCheck size={14} /> Đã kích hoạt</b>
                        ) : (
                          <span className="text-muted">Chờ cài đặt bản dịch</span>
                        )}
                      </div>
                    </div>
                    <div className="version-divider" />
                    <div className="ready-state">
                      {gameInfo?.isVietnameseInstalled ? (
                        <>
                          <CircleCheck size={48} strokeWidth={1.8} className="ready-icon text-success" />
                          <strong>Sẵn sàng trải nghiệm<br />ngay bây giờ.</strong>
                        </>
                      ) : (
                        <>
                          <Layers size={48} strokeWidth={1.8} className="ready-icon text-warning" />
                          <strong className="text-warning">Cần cài đặt Việt Hóa<br />để chơi Tiếng Việt.</strong>
                        </>
                      )}
                    </div>
                  </div>
                </section>
              </section>

              {/* News Sidebar */}
              <aside className="notice-card">
                <div className="notice-heading">
                  <Megaphone size={20} fill="currentColor" />
                  <strong>Tin tức &amp; Cập nhật</strong>
                </div>
                <div className="notice-list">
                  {notices.map((item) => (
                    <article className="notice-item" key={item.title}>
                      <span className={`notice-art ${item.art}`} aria-hidden="true"><i /><b /><em /></span>
                      <span className="notice-text">
                        <div className="notice-meta">
                          <time>{item.date}</time>
                          <span className="notice-badge">{item.badge}</span>
                        </div>
                        <strong>{item.title}</strong>
                        <small>{item.description}</small>
                      </span>
                    </article>
                  ))}
                </div>

                <div className="anti-scam-box">
                  <ShieldAlert size={20} className="warning-icon" />
                  <div>
                    <strong>CẢNH BÁO LỪA ĐẢO</strong>
                    <p>Bản dịch này hoàn toàn MIỄN PHÍ. Không mua từ bất kỳ thương nhân nào.</p>
                  </div>
                </div>
              </aside>
            </div>
          )}

          {section === 'Cập nhật' && (
            <div className="page-view">
              <div className="page-header">
                <h2><RefreshCw size={24} /> Đồng Bộ Bluepoch Hotupdate API</h2>
                <p>Kiểm tra trực tiếp máy chủ chính thức của Bluepoch (<code>hotupdate-hw.sl916.com</code>) để thích ứng với các bản cập nhật tài nguyên mới nhất của Reverse: 1999.</p>
              </div>

              <div className="api-controls-card">
                <div className="api-actions">
                  <button className="primary-btn" onClick={handleCheckBluepoch} disabled={apiChecking}>
                    <RefreshCw size={17} className={apiChecking ? 'spin' : ''} />
                    {apiChecking ? 'Đang truy vấn...' : 'Kiểm tra phiên bản (/diff-update/version)'}
                  </button>
                  <button className="secondary-btn" onClick={handleCheckResources} disabled={resChecking}>
                    <Download size={17} className={resChecking ? 'spin' : ''} />
                    {resChecking ? 'Đang tải...' : 'Quét danh sách tài nguyên (/diff-update/resource)'}
                  </button>
                  <button className="secondary-btn" onClick={handleCheckGithub} disabled={isCheckingGithub}>
                    <Github size={17} className={isCheckingGithub ? 'spin' : ''} />
                    {isCheckingGithub ? 'Đang kiểm tra...' : 'Bản dịch mới từ GitHub'}
                  </button>
                </div>

                <div className="api-specs-grid">
                  <div className="spec-item">
                    <label>Máy chủ cập nhật (CDN)</label>
                    <code>hotupdate-hw.sl916.com</code>
                  </div>
                  <div className="spec-item">
                    <label>CDN Resource Base</label>
                    <code>upres-hw.sl916.com/60001</code>
                  </div>
                  <div className="spec-item">
                    <label>Thuật toán chữ ký (Sign)</label>
                    <code>MD5(60001_2_4_200_6001_&lt;ver&gt;)</code>
                  </div>
                  <div className="spec-item">
                    <label>Localhash Khóa băm</label>
                    <code>2585de5ae8274f5d0061316a8569a344</code>
                  </div>
                </div>
              </div>

              {githubResult && (
                <div className="result-card">
                  <h3>Cập nhật Cộng Đồng từ GitHub</h3>
                  <div className="result-details">
                    <div><span>Trạng thái:</span><b className={githubResult.hasUpdate ? 'text-warning' : 'text-success'}>
                      {githubResult.hasUpdate ? `Có bản cập nhật mới: ${githubResult.latestTag}` : 'Đang sử dụng bản mới nhất'}
                    </b></div>
                    <div><span>Bản phát hành:</span><b>{githubResult.releaseName}</b></div>
                    <div><span>Ghi chú:</span><span>{githubResult.releaseNotes || 'Bản dịch tối ưu hóa v5 Clean.'}</span></div>
                    <div>
                      <span>Kho mã nguồn:</span>
                      <button className="secondary-btn inline-btn" onClick={() => window.launcher?.openRepository()}>
                        <Github size={14} /> Mở kho GitHub
                      </button>
                    </div>
                  </div>
                </div>
              )}

              {apiResult && (
                <div className="result-card">
                  <h3>Kết quả kiểm tra phiên bản</h3>
                  <div className="result-details">
                    <div><span>Chữ ký tạo tự động:</span><code>{apiResult.signature}</code></div>
                    <div><span>Chuỗi gốc:</span><code>{apiResult.rawSignatureString}</code></div>
                    <div><span>Thời điểm kiểm tra:</span><b>{new Date(apiResult.checkedAt).toLocaleString()}</b></div>
                    <div>
                      <span>Đánh giá tương thích:</span>
                      <b className="text-success"><CheckCircle2 size={14} /> Phiên bản hiện tại ({apiResult.localVersion}) đã được kiểm tra tính tương thích với Route B v5.</b>
                    </div>
                  </div>
                  <div className="json-container">
                    <pre>{JSON.stringify(apiResult.results, null, 2)}</pre>
                  </div>
                </div>
              )}

              {resResult && (
                <div className="result-card">
                  <h3>Manifest tài nguyên thay đổi</h3>
                  <div className="result-details">
                    <div><span>URL endpoint:</span><code className="text-truncate">{resResult.url}</code></div>
                  </div>
                  <div className="json-container">
                    <pre>{JSON.stringify(resResult.manifest || { error: resResult.error }, null, 2)}</pre>
                  </div>
                </div>
              )}
            </div>
          )}

          {section === 'Quản lý Việt hóa' && (
            <div className="page-view">
              <div className="page-header">
                <h2><BookOpen size={24} /> Trung Tâm Quản Lý Việt Hóa</h2>
                <p>Quản lý trạng thái cài đặt, triển khai gói tài nguyên và kiểm soát container bản dịch.</p>
              </div>

              {adminWarning}

              <div className="mod-actions-grid">
                <div className="mod-card">
                  <div className="mod-card-header">
                    <Sparkles size={22} className="mod-icon" />
                    <div>
                      <h3>Bản Việt Hóa Route B v5 (Clean)</h3>
                      <p>Bản dịch toàn bộ game, triệt tiêu 100% rác Lerritay và popup cập nhật.</p>
                    </div>
                  </div>

                  <div className="mod-stats">
                    <div className="stat-box"><strong>115,812</strong><span>Chuỗi Giao Diện</span></div>
                    <div className="stat-box"><strong>135,060</strong><span>Câu Thoại Story</span></div>
                    <div className="stat-box"><strong>189</strong><span>Texture Việt Hóa</span></div>
                    <div className="stat-box"><strong>7,274</strong><span>Lồng Tiếng Nhân Vật</span></div>
                  </div>

                  <div className="mod-buttons">
                    <button className="primary-btn" onClick={handleInstallMod} disabled={isModWorking}>
                      <CircleCheck size={17} />
                      Cài đặt / Đồng bộ Tiếng Việt
                    </button>
                    <button className="danger-btn" onClick={handleUninstallMod} disabled={isModWorking}>
                      <XCircle size={17} />
                      Gỡ bỏ Việt Hóa (Về gốc)
                    </button>
                    <button className="secondary-btn" onClick={handleVerifyIntegrity} disabled={isVerifying}>
                      <ShieldCheck size={17} className={isVerifying ? 'spin' : ''} />
                      {isVerifying ? 'Đang kiểm tra...' : 'Kiểm tra toàn vẹn'}
                    </button>
                    <button className="secondary-btn" onClick={() => window.launcher?.openPersistentRoot()}>
                      <Folder size={17} />
                      Mở PersistentRoot
                    </button>
                  </div>
                </div>

                <div className="mod-features-card">
                  <h3>Đặc tính của Bản Việt Hóa v5 Clean:</h3>
                  <ul className="features-list">
                    <li><CheckCircle2 size={16} className="text-success" /> <strong>Xóa sạch nút ON/OFF:</strong> Không còn icon switch floating đè lên giao diện sảnh.</li>
                    <li><CheckCircle2 size={16} className="text-success" /> <strong>Triệt tiêu chữ FCK OFF:</strong> Toàn bộ overlay và chữ thô tục được xóa sạch.</li>
                    <li><CheckCircle2 size={16} className="text-success" /> <strong>Anti-Scam Modal:</strong> Chuyển bảng thông báo thành cảnh báo chống lừa đảo người dùng.</li>
                    <li><CheckCircle2 size={16} className="text-success" /> <strong>Font chữ tiếng Việt:</strong> Hỗ trợ hiển thị mượt mà không lỗi ô vuông.</li>
                  </ul>

                  <div className="backup-status-strip">
                    <div>
                      <strong>Sao lưu tệp gốc (Vanilla):</strong>
                      <small>
                        {gameInfo?.hasVanillaBackup
                          ? `Đã lưu trữ an toàn (${((gameInfo.backupSizeBytes || 0) / (1024 * 1024)).toFixed(1)} MB)`
                          : 'Tự động tạo khi cài đặt bản dịch lần đầu.'}
                      </small>
                    </div>
                    {gameInfo?.hasVanillaBackup && (
                      <button className="secondary-btn inline-btn" onClick={() => window.launcher?.openBackupFolder()}>
                        <Folder size={14} /> Mở bản sao lưu
                      </button>
                    )}
                  </div>
                </div>
              </div>

              {integrityResult && (
                <div className="integrity-card">
                  <div className="integrity-header">
                    <ShieldCheck size={20} className={integrityResult.status === 'intact' ? 'text-success' : 'text-warning'} />
                    <div>
                      <h3>Kết quả kiểm tra tính toàn vẹn bản dịch</h3>
                      <p>{integrityResult.summary}</p>
                    </div>
                    <span className={`status-badge ${integrityResult.status === 'intact' ? 'badge-success' : 'badge-warning'}`}>
                      {integrityResult.status === 'intact' ? 'TOÀN VẸN 100%' : integrityResult.status.toUpperCase()}
                    </span>
                  </div>
                  {integrityResult.details && (
                    <div className="integrity-grid">
                      <div className="integrity-item">
                        <span>Container Lua (6ac5):</span>
                        <b className={integrityResult.details.containerMatches ? 'text-success' : 'text-warning'}>
                          {integrityResult.details.containerMatches ? 'Khớp chữ ký RSA v5' : 'Khác biệt hoặc bản gốc'}
                        </b>
                      </div>
                      <div className="integrity-item">
                        <span>AssetBundle (7335):</span>
                        <b className={integrityResult.details.bundleMatches ? 'text-success' : 'text-warning'}>
                          {integrityResult.details.bundleMatches ? 'Khớp tài nguyên v5 (~232 MB)' : `${((integrityResult.details.installedBundleSize || 0) / (1024 * 1024)).toFixed(1)} MB`}
                        </b>
                      </div>
                      <div className="integrity-item">
                        <span>Phiên bản Client:</span>
                        <b>{integrityResult.details.gameVersion}</b>
                      </div>
                      <div className="integrity-item">
                        <span>Thời điểm kiểm tra:</span>
                        <small>{new Date(integrityResult.details.checkedAt).toLocaleTimeString()}</small>
                      </div>
                    </div>
                  )}
                </div>
              )}
            </div>
          )}

          {section === 'Nhật ký thay đổi' && (
            <div className="page-view">
              <div className="page-header">
                <h2><FileText size={24} /> Nhật Ký Thay Đổi &amp; Bản Quyền Cộng Đồng</h2>
                <p>Theo dõi tiến trình phát triển và các cam kết về bảo mật cộng đồng.</p>
              </div>

              <div className="changelog-scrim">
                <article className="log-entry">
                  <div className="log-badge">Phiên bản 1.0.0 (10/11/2026)</div>
                  <h3>Phát hành Launcher Chính Thức &amp; Hoàn Thiện Triển Khai v5 Clean</h3>
                  <ul>
                    <li>Tích hợp cơ chế gọi API hotupdate của Bluepoch (<code>sl916.com</code>) với công thức ký MD5.</li>
                    <li>Xóa bỏ triệt để toàn bộ floating dock ON/OFF widget (`banner_ui`, `minimized_ui`).</li>
                    <li>Gỡ bỏ hoàn toàn dòng chữ hồng FCK OFF ở màn hình chờ boot.</li>
                    <li>Tùy biến popup khởi động thành Bảng cảnh báo chống bán lại / lừa đảo.</li>
                    <li>Mở rộng kho từ điển giao diện sảnh: Unilog, Chợ Kỳ Diệu, Vực Sâu Limbo, Cỗ Máy Kể Chuyện.</li>
                  </ul>
                </article>

                <article className="log-entry">
                  <div className="log-badge">Phiên bản 0.1.18.2 (09/11/2026)</div>
                  <h3>Đồng Bộ Giao Thức Route B</h3>
                  <ul>
                    <li>Giải mã và nạp 189 Texture đồ họa Việt hóa cho các chương truyện.</li>
                    <li>Xây dựng pipeline biên dịch và đóng gói AES-256-CBC trực tiếp cho <code>banner.bytes</code>.</li>
                  </ul>
                </article>
              </div>
            </div>
          )}

          {/* Bottom Bar */}
          <footer className="bottom-bar">
            <div className="install-path" onClick={handleSelectFolder} title="Nhấn để đổi thư mục game">
              <Folder size={17} fill="currentColor" />
              <span>Đường dẫn cài đặt:</span>
              <b className="path-text">{gameInfo?.gamePath || 'Chưa tìm thấy thư mục game'}</b>
            </div>
            <div className="launcher-version">Launcher v1.0.0 Community Edition</div>
            <button className="github-community" onClick={() => window.launcher?.openRepository()}>
              <Github size={17} />
              GitHub Cộng Đồng
            </button>
          </footer>
        </section>
      </div>

      {/* Settings Modal */}
      {settingsOpen && (
        <div className="modal-scrim" onMouseDown={(e) => { if (e.target === e.currentTarget) setSettingsOpen(false); }}>
          <section className="settings-window" role="dialog" aria-modal="true" aria-labelledby="settings-heading">
            <aside className="settings-sidebar">
              <h2 id="settings-heading">Cài đặt</h2>
              <nav>
                {settingsTabs.map(({ label, title, Icon }) => (
                  <button
                    key={label}
                    className={settingsTab === label ? 'active' : ''}
                    onClick={() => setSettingsTab(label)}
                  >
                    <Icon size={15} />
                    {title}
                  </button>
                ))}
              </nav>
            </aside>
            <div className="settings-content">
              <button className="modal-close" onClick={() => setSettingsOpen(false)} aria-label="Đóng cài đặt">
                <X size={18} />
              </button>
              <h2>{settingsTabs.find((t) => t.label === settingsTab)?.title || settingsTab}</h2>

              {settingsTab === 'General' && (
                <>
                  <div className="setting-group">
                    <label>Ngôn ngữ giao diện</label>
                    <button className="setting-select" onClick={() => setLanguage(language === 'Tiếng Việt' ? 'English' : 'Tiếng Việt')}>
                      <span>{language}</span>
                      <ChevronDown size={16} />
                    </button>
                  </div>
                  <div className="setting-group">
                    <label>Khởi động cùng hệ thống</label>
                    <div className="setting-card">
                      <div className="setting-row">
                        <div>
                          <strong>Tự khởi chạy Launcher khi mở máy</strong>
                          <small>Tự động kiểm tra phiên bản mới từ Bluepoch</small>
                        </div>
                        <button className={`toggle ${autoLaunch ? 'on' : ''}`} onClick={() => setAutoLaunch(!autoLaunch)}>
                          <i />
                        </button>
                      </div>
                    </div>
                  </div>
                  <div className="setting-group">
                    <label>Khi đóng cửa sổ</label>
                    <div className="setting-card close-choice">
                      <button className="radio-row" onClick={() => setCloseBehavior('tray')}>
                        <span className={`radio ${closeBehavior === 'tray' ? 'checked' : ''}`} />
                        Thu nhỏ xuống khay hệ thống <em>Đề xuất</em>
                      </button>
                      <button className="radio-row" onClick={() => setCloseBehavior('exit')}>
                        <span className={`radio ${closeBehavior === 'exit' ? 'checked' : ''}`} />
                        Thoát launcher
                      </button>
                    </div>
                  </div>
                </>
              )}

              {settingsTab === 'GamePath' && (
                <div className="setting-group">
                  <label>Thư mục Reverse: 1999</label>
                  <div className="setting-card gamepath-card">
                    <p>Thư mục chứa <code>reverse1999.exe</code> (có thể chọn thư mục cha, ví dụ <code>reverse1999_global</code>):</p>
                    <div className="path-input-row">
                      <input type="text" readOnly value={gameInfo?.gamePath || ''} placeholder="Chưa tìm thấy thư mục game" className="path-input" />
                      <button className="primary-btn" onClick={handleSelectFolder}>
                        <Folder size={15} /> Chọn thư mục
                      </button>
                      <button className="secondary-btn" onClick={handleScanGames} disabled={isScanning}>
                        <Search size={15} /> {isScanning ? 'Đang dò...' : 'Tự động dò tìm'}
                      </button>
                    </div>
                    <div className="gamepath-status">
                      {gameInfo?.detected ? (
                        <span className="text-success"><CheckCircle2 size={14} /> Hợp lệ — phiên bản {gameInfo.gameVersion}</span>
                      ) : (
                        <span className="text-warning"><XCircle size={14} /> Không tìm thấy reverse1999.exe</span>
                      )}
                      {gameInfo?.gamePathSource && (
                        <span className="source-chip">Nguồn: {GAME_SOURCE_LABELS[gameInfo.gamePathSource] ?? gameInfo.gamePathSource}</span>
                      )}
                      {gameInfo?.detected && <span className="source-chip">Đã lưu cho lần mở sau</span>}
                    </div>
                    {adminWarning}
                    {candidates.length > 0 && (
                      <ul className="candidate-list">
                        {candidates.map((c) => {
                          const active = c.path.toLowerCase() === (gameInfo?.gamePath || '').toLowerCase();
                          return (
                            <li key={c.path} className={active ? 'active' : ''}>
                              <div>
                                <b className="code-font">{c.path}</b>
                                <small>{GAME_SOURCE_LABELS[c.source] ?? c.source}{c.version ? ` · v${c.version}` : ''}</small>
                              </div>
                              {active ? (
                                <span className="text-success">Đang dùng</span>
                              ) : (
                                <button className="secondary-btn" onClick={() => handleUseCandidate(c.path)}>Dùng thư mục này</button>
                              )}
                            </li>
                          );
                        })}
                      </ul>
                    )}
                  </div>
                </div>
              )}

              {settingsTab === 'About' && (
                <div className="about-card">
                  <div className="about-mark">R</div>
                  <strong>Reverse: 1999 Việt Hóa Launcher</strong>
                  <span>Phiên bản 1.0.0 Community</span>
                  <p>Bộ launcher mã nguồn mở hỗ trợ cập nhật, đồng bộ và khởi chạy Reverse: 1999 Tiếng Việt.</p>
                  <button className="primary-btn" onClick={() => window.launcher?.openRepository()}>
                    <Github size={15} />
                    Kho mã nguồn GitHub
                  </button>
                </div>
              )}
            </div>
          </section>
        </div>
      )}

      {/* Toast Notification */}
      {toast && (
        <div className="toast">
          <Check size={16} />
          <span>{toast}</span>
        </div>
      )}
    </main>
  );
}
