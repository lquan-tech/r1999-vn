# viLauncher — Trình Khởi Chạy & Quản Lý Bản Dịch Việt Hóa Reverse: 1999

Ứng dụng mã nguồn mở quản lý cài đặt, kiểm tra toàn vẹn, cập nhật và đồng bộ bản dịch Tiếng Việt cho tựa game **Reverse: 1999** (Global PC Client), xây dựng trên nền tảng **Electron 33 + React 18 + Vite + TypeScript**.

Ứng dụng áp dụng kiến trúc **Route B v5 Clean**: hoạt động độc lập qua cơ chế ToLua hook và AssetBundle trong thư mục `PersistentRoot`, hoàn toàn không sửa đổi tệp cài đặt gốc của game.

---

## 🌟 Tính Năng Nổi Bật

1. **Tự động nhận diện thư mục game (Zero-Config Game Discovery)**:
   - Tự động quét Windows Registry (`HKLM` / `HKCU` Uninstall records).
   - Quét thư viện Steam (`libraryfolders.vdf`).
   - Tự động quét các ổ đĩa máy tính (`C:\` .. `Z:\`) với độ sâu thư mục an toàn.
   - Hỗ trợ chọn thư mục thủ công và lưu cấu hình lâu dài tại `%APPDATA%`.

2. **Cấp quyền UAC tức thời (Just-In-Time Elevation)**:
   - Khi game cài trong phân vùng bảo vệ (`C:\Program Files (x86)\...`), launcher **không bắt buộc** người dùng phải chạy toàn bộ app bằng Admin.
   - Khi bấm **Cài đặt** hoặc **Gỡ bỏ**, Windows sẽ hiển thị bảng xác nhận UAC (Yes/No) tức thời để cấp quyền ghi tệp mà không cần khởi động lại launcher.

3. **Đồng bộ Hotupdate API Bluepoch (`hotupdate-hw.sl916.com`)**:
   - Tự động trích xuất phiên bản game từ hash định danh.
   - Tạo chữ ký MD5 chuẩn giao thức Bluepoch: `md5("60001_2_4_200_6001_" + version)`.
   - Kiểm tra trực tiếp các gói cập nhật vá lỗi (diff patch & resource manifests) từ server chính thức của nhà phát hành.

4. **Kiểm tra toàn vẹn bản dịch (Integrity Diagnostic)**:
   - Quét mã băm MD5 của container Lua hook ToLua và dung lượng bundle tiếng Việt.
   - Phát hiện chính xác nếu game vừa cập nhật đè làm mất bản dịch.

5. **Quản lý sao lưu gốc & Khôi phục 1-Click (Vanilla Backup & Rollback)**:
   - Tự động tạo bản sao lưu của tệp game nguyên bản vào `%APPDATA%\...\vanilla_backup`.
   - Nút **Gỡ bỏ Việt Hóa** cho phép hoàn tác 100% về game gốc bất cứ lúc nào.

6. **Giao diện Reverse: 1999 Dark Gold Anime & Kính Mờ (Glassmorphism)**:
   - Phong cách nghệ thuật cổ điển tông vàng kim - đen mun đặc trưng của Reverse: 1999.
   - Tích hợp tab Cảnh Báo Lừa Đảo (Anti-Scam Modal) bảo vệ cộng đồng khỏi các đối tượng thương mại hóa trái phép bản dịch.

---

## 📁 Cấu Trúc Thư Mục Repository

```text
viLauncher/
├── R199Launc/                          # Mã nguồn ứng dụng Launcher chính
│   ├── electron/                       # Backend Electron (Node.js/TypeScript)
│   │   ├── main.ts                     # IPC handlers, UAC elevation, game detection, API Bluepoch
│   │   └── preload.ts                  # Cầu nối IPC an toàn (ContextBridge)
│   ├── src/                            # Giao diện React (Vite + TypeScript)
│   │   ├── App.tsx                     # 5 Tabs: Tổng quan, Đồng bộ API, Quản lý Việt hóa, Nhật ký, Cài đặt
│   │   ├── main.tsx                    # Entrypoint React 18
│   │   ├── style.css                   # Stylesheet giao diện Dark Gold & Glassmorphism
│   │   └── electron.d.ts               # Typings TypeScript cho Window.launcher
│   ├── index.html                      # Khung HTML gốc
│   ├── package.json                    # Cấu hình dependency & build scripts
│   ├── tsconfig.json                   # Cấu hình TypeScript compiler
│   └── vite.config.ts                  # Cấu hình bundler Vite
├── deploy_route_b_full_vietnamese_v5.py # Kịch bản Python triển khai nhanh Route B v5 độc lập
├── package_exe.mjs                     # Script tự động build & đóng gói viLauncher.exe / viLauncher2.exe
├── HUONG_DAN_KIEN_TRUC_VIET_HOA_REVERSE1999.md # Tài liệu chi tiết kiến trúc Route B & ToLua hook
├── REPORT.md                           # Báo cáo dịch ngược và phân tích giao thức game
└── README.md                           # Tài liệu tổng quan dự án
```

---

## 🛠️ Hướng Dẫn Cài Đặt & Phát Triển

### Yêu Cầu Môi Trường
- **Node.js**: Phiên bản 18.x trở lên (khuyên dùng Node 20+ hoặc 22+).
- **npm**: Phiên bản 9.x trở lên.
- **Python**: 3.10+ (nếu sử dụng script deploy bổ trợ).
- **Hệ điều hành**: Windows 10 / 11 (64-bit).

### 1. Cài đặt Dependencies
```bash
cd R199Launc
npm install
```

### 2. Chạy môi trường Phát Triển (Dev Mode)
```bash
npm run dev
```

### 3. Biên dịch Mã Nguồn (Frontend & Electron)
```bash
npm run build
```

### 4. Đóng Gói Tự Động Ra File Executable (.exe)
Tại thư mục gốc dự án:
```bash
node package_exe.mjs
```
Script sẽ tự động:
- Biên dịch toàn bộ frontend React và backend Electron.
- Đóng gói installer bằng `electron-builder` (NSIS target).
- Tự động áp dụng cơ chế versioning: tạo `viLauncher.exe` làm bản backup và `viLauncher2.exe` làm bản vá cập nhật mới nhất.

---

## 📚 Tài Liệu Kỹ Thuật Bổ Trợ
- [HUONG_DAN_KIEN_TRUC_VIET_HOA_REVERSE1999.md](HUONG_DAN_KIEN_TRUC_VIET_HOA_REVERSE1999.md): Giải thích chi tiết cơ chế ToLua Hook, mã hóa AES-256-CBC, cấu trúc carrier polyglot và cách thức hoạt động của thư mục `PersistentRoot`.
- [REPORT.md](REPORT.md): Báo cáo dịch ngược mã nguồn launcher cũ và đặc tả giao thức API Bluepoch.

---

## ⚖️ Tuyên Bố Miễn Trừ Trách Nhiệm (Disclaimer)
- Dự án này được phát triển hoàn toàn vì mục đích phi lợi nhuận nhằm hỗ trợ cộng đồng người chơi Việt Nam trải nghiệm cốt truyện tựa game Reverse: 1999 một cách trọn vẹn nhất.
- Tất cả tài sản, hình ảnh, âm thanh và thương hiệu Reverse: 1999 thuộc quyền sở hữu của **Bluepoch Co., Ltd.**
- Dự án không can thiệp vào mã thực thi gốc (`reverse1999.exe`), không hack/cheat, không sửa đổi dữ liệu tài khoản và hoàn toàn tuân thủ các nguyên tắc an toàn cho người chơi.
