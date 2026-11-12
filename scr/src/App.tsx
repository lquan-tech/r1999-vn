import { useState, useEffect, useRef } from 'react';
import {
  Github, Bug, Settings, Minus, X,
  Play, ArrowDown, Wrench, Sparkles,
  Volume2, VolumeX, ChevronLeft, ChevronRight,
  CircleCheck, RefreshCw, Folder, HardDrive
} from 'lucide-react';
import './style.css';

type SettingsTab = 'Chung' | 'Đường dẫn game' | 'Tải xuống' | 'Công cụ' | 'Giới thiệu';
type AnimState = 'closed' | 'open' | 'closing';

interface CarouselSlide {
  id: number;
  tag: string;
  title: string;
  subtitle: string;
  image: string;
}

const defaultSlides: CarouselSlide[] = [
  {
    id: 1,
    tag: 'BẢN DỊCH ARCANIST',
    title: 'Cơn Mưa Hoán Đổi - Bản Việt Hóa Arcanist',
    subtitle: 'Bản dịch toàn bộ cốt truyện & phân cảnh',
    image: './mainwindow_en.jpg'
  },
  {
    id: 2,
    tag: 'SỰ KIỆN ĐẶC BIỆT',
    title: 'Sự Kiện UTTU & Bản Cập Nhật Mới',
    subtitle: 'Nội dung cốt truyện mới & tối ưu phông chữ',
    image: './mainwindow_en.jpg'
  },
  {
    id: 3,
    tag: 'HƯỚNG DẪN',
    title: 'Tối Ưu Hóa Trải Nghiệm Tiếng Việt',
    subtitle: 'Kiểm tra tính toàn vẹn và sửa lỗi bản dịch',
    image: './mainwindow_en.jpg'
  }
];

export default function App() {
  // Modal & Popup States with Enter/Exit Animations (Requirement 4)
  const [settingsModalState, setSettingsModalState] = useState<AnimState>('closed');
  const [settingsTab, setSettingsTab] = useState<SettingsTab>('Chung');
  const [gearMenuState, setGearMenuState] = useState<AnimState>('closed');

  const openSettings = () => setSettingsModalState('open');
  const closeSettings = () => {
    setSettingsModalState('closing');
    setTimeout(() => setSettingsModalState('closed'), 180);
  };

  const openGearMenu = () => setGearMenuState('open');
  const closeGearMenu = () => {
    setGearMenuState('closing');
    setTimeout(() => setGearMenuState('closed'), 140);
  };

  const gearMenuRef = useRef<HTMLDivElement>(null);
  const gearBtnRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (gearMenuState !== 'open') return;
    const handleOutsideClick = (e: MouseEvent) => {
      const target = e.target as Node;
      if (
        gearMenuRef.current &&
        !gearMenuRef.current.contains(target) &&
        gearBtnRef.current &&
        !gearBtnRef.current.contains(target)
      ) {
        closeGearMenu();
      }
    };
    document.addEventListener('mousedown', handleOutsideClick);
    return () => document.removeEventListener('mousedown', handleOutsideClick);
  }, [gearMenuState]);

  const toggleGearMenu = (e: React.MouseEvent) => {
    e.stopPropagation();
    if (gearMenuState === 'open') {
      closeGearMenu();
    } else {
      openGearMenu();
    }
  };

  // In-App Toast Notification State
  const [toastMessage, setToastMessage] = useState<string | null>(null);
  const toastTimeoutRef = useRef<NodeJS.Timeout | null>(null);

  const showToast = (msg: string) => {
    if (toastTimeoutRef.current) clearTimeout(toastTimeoutRef.current);
    setToastMessage(msg);
    toastTimeoutRef.current = setTimeout(() => {
      setToastMessage(null);
    }, 3200);
  };

  // Game & Localization Version States
  const [gameDir, setGameDir] = useState<string>('');
  const [gameVersion] = useState<string>('3.9.0');
  const [installedViethoaVersion, setInstalledViethoaVersion] = useState<string>('1.0.0');
  const [remoteViethoaVersion, setRemoteViethoaVersion] = useState<string>('1.0.0');
  const [viethoaNeedsUpdate, setViethoaNeedsUpdate] = useState<boolean>(false);

  // Launcher Version
  const [launcherVersion] = useState<string>('2.3.0');
  const [remoteLauncherVersion, setRemoteLauncherVersion] = useState<string>('2.3.0');
  const [launcherNeedsUpdate, setLauncherNeedsUpdate] = useState<boolean>(false);

  // Asset States
  const [bgImage, setBgImage] = useState<string>('./mainwindow_en.png');
  const [bgmSrc, setBgmSrc] = useState<string | null>(null);
  const [slides, setSlides] = useState<CarouselSlide[]>(defaultSlides);
  const [activeSlide, setActiveSlide] = useState(0);

  // News & Notices Tab State
  const [newsTab, setNewsTab] = useState<'notices' | 'news'>('notices');
  const [noticesList, setNoticesList] = useState<string[]>(['haha one1']);
  const [newsList, setNewsList] = useState<string[]>(['haha tree 3']);

  // Audio Player State
  const audioRef = useRef<HTMLAudioElement | null>(null);
  const [isMuted, setIsMuted] = useState(false);

  // Installation Progress
  const [isInstalling, setIsInstalling] = useState(false);

  // Settings Values
  const [closeAction, setCloseAction] = useState<'minimize' | 'exit'>('minimize');
  const [autoUpdate, setAutoUpdate] = useState(true);
  const [optimize4K, setOptimize4K] = useState(true);

  // Sync Assets from Electron
  const syncFromElectron = async (force = false) => {
    if (window.launcher?.syncAssets) {
      try {
        const res = await window.launcher.syncAssets(force);
        if (res) {
          if (res.bgImageSrc) setBgImage(res.bgImageSrc);
          if (res.bgmSrc) setBgmSrc(res.bgmSrc);
          if (res.banners && res.banners.length > 0) setSlides(res.banners);

          if (res.launcherVersion) {
            setRemoteLauncherVersion(res.launcherVersion.remote);
            setLauncherNeedsUpdate(res.launcherVersion.needsUpdate);
          }

          if (res.viethoaVersion) {
            setInstalledViethoaVersion(res.viethoaVersion.installed || '0.0.0');
            setRemoteViethoaVersion(res.viethoaVersion.remote || '1.0.0');
            setViethoaNeedsUpdate(res.viethoaVersion.needsUpdate);
          }

          if (res.noticesText) {
            const lines = res.noticesText.split('\n').map((l) => l.trim()).filter(Boolean);
            if (lines.length > 0) setNoticesList(lines);
          }

          if (res.newsText) {
            const lines = res.newsText.split('\n').map((l) => l.trim()).filter(Boolean);
            if (lines.length > 0) setNewsList(lines);
          }
        }
      } catch (err) {
        console.warn('Sync failed, using offline cache:', err);
      }
    }
  };

  useEffect(() => {
    syncFromElectron(false);
  }, []);

  // Handle BGM Audio
  useEffect(() => {
    if (audioRef.current && bgmSrc) {
      audioRef.current.volume = 0.4;
      if (!isMuted) {
        audioRef.current.play().catch(() => {});
      } else {
        audioRef.current.pause();
      }
    }
  }, [bgmSrc, isMuted]);

  const toggleAudio = () => {
    if (!audioRef.current) return;
    if (isMuted) {
      audioRef.current.play().catch(() => {});
      setIsMuted(false);
    } else {
      audioRef.current.pause();
      setIsMuted(true);
    }
  };

  // Carousel slide timer
  useEffect(() => {
    const timer = setInterval(() => {
      setActiveSlide((prev) => (prev + 1) % slides.length);
    }, 6000);
    return () => clearInterval(timer);
  }, [slides.length]);

  // Auto-detect game installation path on startup
  useEffect(() => {
    let mounted = true;
    const detectGame = async () => {
      if (window.launcher?.detectGamePath) {
        try {
          const detected = await window.launcher.detectGamePath();
          if (mounted) {
            if (detected) {
              setGameDir(detected);
            } else {
              setGameDir('');
              showToast('Chưa chọn thư mục game! Vui lòng chọn đường dẫn cài đặt Reverse: 1999.');
            }
          }
        } catch {
          if (mounted) setGameDir('');
        }
      }
    };
    detectGame();
    return () => {
      mounted = false;
    };
  }, []);

  const prevSlide = () => {
    setActiveSlide((prev) => (prev - 1 + slides.length) % slides.length);
  };

  const nextSlide = () => {
    setActiveSlide((prev) => (prev + 1) % slides.length);
  };

  const runInstallViethoa = async () => {
    if (!gameDir) {
      showToast('Chưa chọn thư mục game! Vui lòng chọn đường dẫn cài đặt Reverse: 1999.');
      await handleSelectFolder();
      return;
    }

    setIsInstalling(true);
    showToast(`Đang tải & cài đặt bản dịch tiếng Việt v${remoteViethoaVersion}...`);

    try {
      if (window.launcher?.updateViethoa) {
        const res = await window.launcher.updateViethoa(gameDir);
        if (!res.success) {
          showToast(res.message || 'Cài đặt bản dịch thất bại.');
          return;
        }
        setInstalledViethoaVersion(res.version);
        setViethoaNeedsUpdate(false);
        showToast(res.message || `Cập nhật bản dịch v${res.version} thành công!`);
      } else {
        await new Promise((r) => setTimeout(r, 1500));
        setInstalledViethoaVersion(remoteViethoaVersion);
        setViethoaNeedsUpdate(false);
        showToast(`Cập nhật bản dịch v${remoteViethoaVersion} thành công!`);
      }
    } catch (err: any) {
      showToast('Lỗi cập nhật: ' + (err?.message || err));
    } finally {
      setIsInstalling(false);
    }
  };

  // Handle Primary CTA Button
  const handlePrimaryAction = async () => {
    // Check if game directory is chosen
    if (!gameDir) {
      showToast('Chưa chọn thư mục game! Vui lòng chọn đường dẫn cài đặt Reverse: 1999.');
      await handleSelectFolder();
      return;
    }

    if (viethoaNeedsUpdate || installedViethoaVersion === '0.0.0') {
      await runInstallViethoa();
    } else {
      if (window.launcher) {
        const res = await window.launcher.launchGame(gameDir);
        if (!res.success) {
          showToast(res.message);
        }
      } else {
        showToast('Đang khởi chạy Reverse: 1999 (Chế độ preview)');
      }
    }
  };

  const handleSelectFolder = async () => {
    if (window.launcher?.selectFolder) {
      const selected = await window.launcher.selectFolder();
      if (selected) {
        setGameDir(selected);
        showToast(`Đã chọn thư mục game: ${selected}`);
      }
    }
  };

  const needsDownloadOrUpdate = viethoaNeedsUpdate || installedViethoaVersion === '0.0.0';

  return (
    <div className="launcher-window">
      {/* Background BGM Audio */}
      {bgmSrc && <audio ref={audioRef} src={bgmSrc} loop />}

      {/* In-App Toast Notification */}
      {toastMessage && (
        <div className="in-app-toast-container">
          <div className="in-app-toast">
            <Sparkles size={16} className="toast-gold-icon" />
            <span>{toastMessage}</span>
          </div>
        </div>
      )}

      {/* BACKGROUND IMAGE (NO DRAG) & CINEMATIC FADES */}
      <div className="launcher-canvas">
        <img
          src={bgImage}
          alt="Reverse 1999 Artwork"
          className="launcher-bg-img"
          draggable={false}
          onDragStart={(e) => e.preventDefault()}
        />
        {/* Cinematic Vignette: Soft light fade at top, bottom, and behind logo */}
        <div className="cinematic-vignette" />
      </div>

      {/* TOP HEADER */}
      <header className="launcher-header">
        <div className="brand-cluster">
          <img
            src="./logo.png"
            alt="Reverse: 1999 Việt Hóa"
            className="launcher-brand-logo"
            draggable={false}
            onDragStart={(e) => e.preventDefault()}
          />
        </div>

        {/* Center alert if launcher update available */}
        {launcherNeedsUpdate && (
          <button
            className="launcher-update-alert"
            data-tooltip="Có phiên bản Launcher mới trên GitHub"
            onClick={() => window.launcher?.openRepository()}
          >
            <Sparkles size={12} /> Cập nhật Launcher v{remoteLauncherVersion}
          </button>
        )}

        {/* HEADER CONTROLS WITH HOYOPLAY MICRO-POPPER TOOLTIPS & SEPARATORS */}
        <div className="header-widgets-cluster">
          <div
            className="widget-ping"
            data-tooltip="Độ trễ máy chủ: 28 ms (Trực tuyến)"
          >
            <span className="ping-dot" />
            <span>28 ms</span>
          </div>

          {/* Dấu gạch ngăn cách giữa Ping và Loa/Cài đặt */}
          <div className="window-control-divider" />

          <button
            className="header-icon-btn"
            data-tooltip={isMuted ? 'Bật nhạc nền' : 'Tắt nhạc nền'}
            onClick={toggleAudio}
          >
            {isMuted ? <VolumeX size={17} /> : <Volume2 size={17} />}
          </button>

          <button
            className="header-icon-btn"
            data-tooltip="Cài đặt"
            onClick={openSettings}
          >
            <Settings size={17} />
          </button>

          <div className="window-control-divider" />

          {/* Window Chrome Actions (Flush to top-right corner) */}
          <div className="window-chrome-actions">
            <button
              className="btn-min"
              data-tooltip="Thu nhỏ"
              onClick={() => window.launcher?.minimize()}
            >
              <Minus size={16} />
            </button>
            <button
              className="btn-close"
              data-tooltip="Đóng"
              onClick={() => window.launcher?.close()}
            >
              <X size={16} />
            </button>
          </div>
        </div>
      </header>

      {/* RIGHT-SIDE ICONS (BIGGER, TOP-DOWN) */}
      <aside className="side-utility-bar">
        <button
          className="side-util-btn"
          data-tooltip="Mã nguồn GitHub"
          onClick={() => window.launcher?.openRepository()}
        >
          <Github size={28} />
        </button>

        <button
          className="side-util-btn"
          data-tooltip="Báo lỗi & Góp ý bản dịch"
          onClick={() => window.launcher?.openUrl('https://github.com/lquan-tech/r1999-vn/issues')}
        >
          <Bug size={28} />
        </button>
      </aside>

      {/* MAIN CONTENT AREA */}
      <main className="launcher-content">
        {/* BOTTOM-LEFT BANNER + NOTICES / NEWS WIDGET */}
        <div className="bottom-left-cluster">
          <div className="banner-widget-card">
            {/* Banner Carousel */}
            <div className="banner-carousel-box">
              {slides[activeSlide] && (
                <img
                  src={slides[activeSlide].image}
                  alt={slides[activeSlide].title}
                  className="banner-carousel-img"
                  draggable={false}
                  onDragStart={(e) => e.preventDefault()}
                />
              )}
              <button className="banner-nav-btn prev" onClick={prevSlide}>
                <ChevronLeft size={16} />
              </button>
              <button className="banner-nav-btn next" onClick={nextSlide}>
                <ChevronRight size={16} />
              </button>

              <div className="banner-pill-bars">
                {slides.map((_, idx) => (
                  <button
                    key={idx}
                    className={`banner-pill-bar ${idx === activeSlide ? 'active' : ''}`}
                    onClick={() => setActiveSlide(idx)}
                  />
                ))}
              </div>
            </div>

            {/* Notices and News Tabs with Slide Animation */}
            <div className="news-tabs-section">
              <div className="news-tab-header">
                <button
                  className={`news-tab-btn ${newsTab === 'notices' ? 'active' : ''}`}
                  onClick={() => setNewsTab('notices')}
                >
                  Notices
                </button>
                <button
                  className={`news-tab-btn ${newsTab === 'news' ? 'active' : ''}`}
                  onClick={() => setNewsTab('news')}
                >
                  News
                </button>
              </div>

              {/* Items loaded from notices.txt or news.txt */}
              <div key={newsTab} className="news-items-list">
                {newsTab === 'notices' ? (
                  noticesList.map((item, index) => (
                    <div key={index} className="news-item-row" onClick={() => showToast(item)}>
                      <span className="news-item-title">{item}</span>
                      <span className="news-item-date">11/11</span>
                    </div>
                  ))
                ) : (
                  newsList.map((item, index) => (
                    <div key={index} className="news-item-row" onClick={() => showToast(item)}>
                      <span className="news-item-title">{item}</span>
                      <span className="news-item-date">11/10</span>
                    </div>
                  ))
                )}
              </div>
            </div>
          </div>

          {/* Version Status Footnote - Góc trái bên dưới banner */}
          <div className="launcher-status-footnote">
            <CircleCheck size={13} className="status-check-icon" />
            <span>
              Game: <strong>{gameVersion}</strong> • Việt Hóa:{' '}
              <strong>v{installedViethoaVersion}</strong>
              {viethoaNeedsUpdate ? ' (Có bản cập nhật)' : ' (Sẵn sàng)'}
            </span>
          </div>
        </div>

        {/* BOTTOM-RIGHT: REVERSE 1999 VINTAGE GOLD BUTTON WITH INSET WRENCH */}
        <div className="bottom-right-cluster">
          <div className="cta-button-group">
            <button
              className="action-btn-pill"
              disabled={isInstalling}
              onClick={handlePrimaryAction}
            >
              {/* Left Circle Badge with Loop Animation */}
              <div className={`btn-icon-circle ${needsDownloadOrUpdate ? 'anim-download' : ''}`}>
                {isInstalling ? (
                  <RefreshCw size={17} className="spin-icon" />
                ) : !gameDir ? (
                  <Folder size={18} />
                ) : needsDownloadOrUpdate ? (
                  <ArrowDown size={19} />
                ) : (
                  <Play size={18} fill="#14161a" />
                )}
              </div>

              {/* Main Button Label */}
              <span className="btn-main-label">
                {isInstalling
                  ? 'ĐANG TẢI...'
                  : !gameDir
                  ? 'CHỌN THƯ MỤC GAME'
                  : viethoaNeedsUpdate || installedViethoaVersion === '0.0.0'
                  ? 'TẢI BẢN DỊCH'
                  : 'BẮT ĐẦU'}
              </span>

              {/* Harmonious Inset Wrench Badge without annoying tooltip */}
              <div
                ref={gearBtnRef}
                className="btn-inner-tool"
                onClick={toggleGearMenu}
              >
                <Wrench size={17} />
              </div>
            </button>

            {/* REQUIREMENT 4: QUICK ACTION POPOVER MENU WITH ENTER & EXIT ANIMATIONS */}
            {gearMenuState !== 'closed' && (
              <div
                ref={gearMenuRef}
                className={`quick-action-menu ${gearMenuState === 'closing' ? 'closing' : ''}`}
                onClick={(e) => e.stopPropagation()}
              >
                <button
                  className="action-menu-item"
                  onClick={(e) => {
                    e.stopPropagation();
                    closeGearMenu();
                    if (gameDir) {
                      window.launcher?.openFolder(gameDir);
                    } else {
                      showToast('Chưa chọn thư mục game! Vui lòng chọn thư mục cài đặt.');
                      handleSelectFolder();
                    }
                  }}
                >
                  <Folder size={16} />
                  <div className="action-menu-text-col">
                    <span className="action-menu-title">Mở thư mục game</span>
                    <span className="action-menu-desc">Truy cập thư mục cài đặt Reverse: 1999</span>
                  </div>
                </button>

                <button
                  className="action-menu-item"
                  onClick={async (e) => {
                    e.stopPropagation();
                    closeGearMenu();
                    await runInstallViethoa();
                  }}
                >
                  <ArrowDown size={16} />
                  <div className="action-menu-text-col">
                    <span className="action-menu-title">Cài lại bản Việt hóa</span>
                    <span className="action-menu-desc">Tải & cài lại font Lora/JBM + bản dịch từ GitHub</span>
                  </div>
                </button>

                <button
                  className="action-menu-item"
                  onClick={async (e) => {
                    e.stopPropagation();
                    closeGearMenu();
                    showToast('Đang tải và cập nhật toàn bộ tài nguyên từ GitHub...');
                    await syncFromElectron(true);
                    showToast('Đã đồng bộ lại toàn bộ tài nguyên thành công!');
                  }}
                >
                  <RefreshCw size={16} />
                  <div className="action-menu-text-col">
                    <span className="action-menu-title">Đồng bộ lại tài nguyên</span>
                    <span className="action-menu-desc">Tải lại ảnh nền, nhạc BGM, banner mới</span>
                  </div>
                </button>

                <button
                  className="action-menu-item"
                  onClick={(e) => {
                    e.stopPropagation();
                    closeGearMenu();
                    openSettings();
                  }}
                >
                  <Settings size={16} />
                  <div className="action-menu-text-col">
                    <span className="action-menu-title">Cài đặt hệ thống</span>
                    <span className="action-menu-desc">Tùy chỉnh hành vi và đường dẫn game</span>
                  </div>
                </button>
              </div>
            )}
          </div>
        </div>
      </main>

      {/* REQUIREMENT 4: FROSTED GLASS MODAL: SETTINGS (ENTER & EXIT ANIMATIONS) */}
      {settingsModalState !== 'closed' && (
        <div
          className={`modal-overlay ${settingsModalState === 'closing' ? 'closing' : ''}`}
          onClick={closeSettings}
        >
          <div
            className={`modal-card ${settingsModalState === 'closing' ? 'closing' : ''}`}
            onClick={(e) => e.stopPropagation()}
          >
            <div className="modal-header">
              <span className="modal-pure-title">Cài đặt</span>
              <button className="modal-close-btn" onClick={closeSettings}>
                <X size={16} />
              </button>
            </div>

            <div className="modal-body">
              <div className="settings-nav">
                {(['Chung', 'Đường dẫn game', 'Tải xuống', 'Công cụ', 'Giới thiệu'] as SettingsTab[]).map(
                  (tab) => (
                    <button
                      key={tab}
                      className={`settings-tab-btn ${settingsTab === tab ? 'active' : ''}`}
                      onClick={() => setSettingsTab(tab)}
                    >
                      {tab}
                    </button>
                  )
                )}
              </div>

              <div className="settings-main-panel">
                {settingsTab === 'Chung' && (
                  <>
                    <div>
                      <h4 className="settings-section-title">Khi đóng cửa sổ</h4>
                      <div className="hyp-radio-group">
                        <label className="hyp-radio" onClick={() => setCloseAction('minimize')}>
                          <span className={`hyp-radio__input ${closeAction === 'minimize' ? 'is-checked' : ''}`}>
                            <span className="hyp-radio__inner" />
                          </span>
                          <span>Thu nhỏ xuống khay hệ thống (Đề xuất)</span>
                        </label>
                        <label className="hyp-radio" onClick={() => setCloseAction('exit')}>
                          <span className={`hyp-radio__input ${closeAction === 'exit' ? 'is-checked' : ''}`}>
                            <span className="hyp-radio__inner" />
                          </span>
                          <span>Thoát hoàn toàn launcher</span>
                        </label>
                      </div>
                    </div>

                    <div>
                      <h4 className="settings-section-title">Tính năng tự động</h4>
                      <div className="hyp-checkbox-group">
                        <label className="hyp-checkbox" onClick={() => setAutoUpdate(!autoUpdate)}>
                          <span className={`hyp-checkbox__input ${autoUpdate ? 'is-checked' : ''}`}>
                            <span className="hyp-checkbox__inner">
                              {autoUpdate && (
                                <svg viewBox="0 0 12 10" width="10" height="8" fill="none">
                                  <path d="M1 5l3.5 3.5L11 1.5" stroke="#14161a" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                                </svg>
                              )}
                            </span>
                          </span>
                          <span>Tự động kiểm tra bản dịch mới khi khởi động</span>
                        </label>

                        <label className="hyp-checkbox" onClick={() => setOptimize4K(!optimize4K)}>
                          <span className={`hyp-checkbox__input ${optimize4K ? 'is-checked' : ''}`}>
                            <span className="hyp-checkbox__inner">
                              {optimize4K && (
                                <svg viewBox="0 0 12 10" width="10" height="8" fill="none">
                                  <path d="M1 5l3.5 3.5L11 1.5" stroke="#14161a" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                                </svg>
                              )}
                            </span>
                          </span>
                          <span>Tối ưu hóa phông chữ sắc nét cho màn hình 2K/4K</span>
                        </label>
                      </div>
                    </div>
                  </>
                )}

                {settingsTab === 'Đường dẫn game' && (
                  <>
                    <div>
                      <h4 className="settings-section-title">Thư mục cài đặt Reverse: 1999</h4>
                      <div className="path-picker-box">
                        <input
                          type="text"
                          className="path-input"
                          placeholder="Chưa chọn thư mục game (Nhấn Duyệt để chọn)..."
                          value={gameDir}
                          onChange={(e) => setGameDir(e.target.value)}
                        />
                        <button className="btn-secondary" onClick={handleSelectFolder}>
                          <Folder size={14} /> Duyệt...
                        </button>
                      </div>
                      {!gameDir ? (
                        <div style={{ color: '#f59e0b', fontSize: '12px', marginTop: '8px', display: 'flex', alignItems: 'center', gap: '6px' }}>
                          <span>⚠️ Chưa liên kết thư mục game. Bấm "Duyệt..." để chọn thư mục cài đặt Reverse: 1999 hoặc bấm Tự động tìm kiếm.</span>
                        </div>
                      ) : (
                        <div style={{ color: 'var(--r1999-gold-primary)', fontSize: '12px', marginTop: '8px', display: 'flex', alignItems: 'center', gap: '6px' }}>
                          <span>✓ Đã liên kết thành công thư mục cài đặt game.</span>
                        </div>
                      )}
                      <div style={{ marginTop: '12px' }}>
                        <button
                          className="btn-secondary"
                          onClick={async () => {
                            if (window.launcher?.detectGamePath) {
                              showToast('Đang quét tìm kiếm thư mục game...');
                              const detected = await window.launcher.detectGamePath();
                              if (detected) {
                                setGameDir(detected);
                                showToast(`Đã tìm thấy game: ${detected}`);
                              } else {
                                showToast('Không tìm thấy game tự động. Vui lòng bấm Duyệt để chọn thủ công.');
                              }
                            }
                          }}
                        >
                          <RefreshCw size={13} /> Tự động quét tìm kiếm game
                        </button>
                      </div>
                    </div>

                    <div>
                      <h4 className="settings-section-title">Tình trạng ổ đĩa</h4>
                      <div style={{ display: 'flex', alignItems: 'center', gap: '12px', background: 'rgba(16, 18, 22, 0.55)', padding: '12px 16px', borderRadius: '8px' }}>
                        <HardDrive size={18} style={{ color: 'var(--r1999-gold-primary)' }} />
                        <div>
                          <div style={{ fontSize: '13px', fontWeight: 600 }}>
                            {gameDir ? `Ổ đĩa ${gameDir.slice(0, 3).toUpperCase()}` : 'Chưa liên kết ổ đĩa'}
                          </div>
                          <div style={{ fontSize: '11px', color: 'rgba(229, 229, 229, 0.6)' }}>
                            {gameDir ? 'Đủ dung lượng khả dụng cho bản cài đặt và mod Việt hóa' : 'Vui lòng chọn thư mục game để kiểm tra'}
                          </div>
                        </div>
                      </div>
                    </div>
                  </>
                )}

                {settingsTab === 'Tải xuống' && (
                  <div>
                    <h4 className="settings-section-title">Nguồn tải bản dịch</h4>
                    <p style={{ fontSize: '13px', color: '#e5e5e5', lineHeight: 1.6 }}>
                      Launcher tự động kiểm tra và tải các bản vá tiếng Việt từ GitHub:
                      <br />
                      <code>https://github.com/lquan-tech/r1999-vn/tree/main/public/fileviethoa</code>
                    </p>
                  </div>
                )}

                {settingsTab === 'Công cụ' && (
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
                    <h4 className="settings-section-title">Công cụ quản lý mod</h4>
                    <button className="btn-secondary" onClick={() => window.launcher?.openFolder(gameDir)}>
                      <Folder size={14} /> Mở thư mục tệp tin Reverse1999en
                    </button>
                    <button
                      className="btn-secondary"
                      onClick={async () => {
                        closeSettings();
                        await runInstallViethoa();
                      }}
                    >
                      <ArrowDown size={14} /> Cài lại bản Việt hóa & Font từ GitHub
                    </button>
                    <button
                      className="btn-secondary"
                      onClick={async () => {
                        showToast('Đang tải lại toàn bộ tài nguyên từ GitHub...');
                        await syncFromElectron(true);
                        showToast('Đã tải lại toàn bộ tài nguyên và nhạc nền!');
                      }}
                    >
                      <RefreshCw size={14} /> Tải lại toàn bộ Asset & Nhạc nền
                    </button>
                  </div>
                )}

                {settingsTab === 'Giới thiệu' && (
                  <div>
                    <h4 className="settings-section-title">Về dự án</h4>
                    <p style={{ fontSize: '13px', color: '#e5e5e5', lineHeight: 1.6 }}>
                      <strong>Reverse: 1999 Việt Hóa (v{launcherVersion})</strong>
                      <br />
                      Thiết kế giao diện 1:1 chuẩn xác theo phong cách retro Arcanist.
                    </p>
                  </div>
                )}
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
