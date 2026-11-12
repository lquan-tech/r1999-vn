# REVERSE: 1999 VIỆT HÓA - LAUNCHER NÂNG CẤP TOÀN DIỆN (v2.3.0)

Dự án Launcher cộng đồng dành riêng cho **Reverse: 1999 Việt Hóa** đã được tái thiết kế và nâng cấp toàn diện dựa trên tiêu chuẩn kiến trúc hiện đại của các launcher AAA (như HoYoPlay).

---

## 🌟 CÁC CẢI TIẾN NỔI BẬT ĐÃ THỰC HIỆN

### 1. Giao Diện Chuẩn Mỹ Thuật Reverse: 1999 (Art Deco & The Storm)
* **Khung Cửa Sổ Acrylic Glassmorphism**: Thiết kế không viền (frameless) 1280x720 với hiệu ứng kính mờ `backdrop-filter: blur(20px)` và bóng đổ sâu sang trọng.
* **Hiệu Ứng Cơn Mưa Ngược (The Storm Rain Effect)**: Các vệt mưa vàng cổ điển rơi nhẹ nhàng trong nền launcher, tạo đúng không khí huyền bí đặc trưng của Reverse: 1999.
* **Bánh Răng Đồng Hồ Cổ (Vintage Clockwork)**: Chi tiết đồng hồ quay chậm theo chu kỳ thời gian ở góc màn hình chính.
* **Bảng Màu Quý Tộc Thế Kỷ 20**: Kết hợp hài hòa giữa màu vàng đồng hoàng gia (`#d4af37`), đỏ mận rượu vang (`#8b263e`), xanh sương mù (`#38bdf8`) và nền đá phiến bóng tối (`#0c0f14`).

### 2. Hệ Thống 4 Tab Đầy Đủ Chức Năng (Không Còn Bị Đóng Băng Như Bản Cũ)
* **Tab 1: Tổng quan (Overview)**:
  * **Carousel Banner Động**: Tự động chuyển đổi các slide sự kiện ("Cơn Mưa Hoán Đổi Chương 5", "Trộm Khắp Luân Đôn", "Nâng Cấp Font Chữ Màn Hình 4K").
  * **Cụm Nút Hành Động Lớn (Hero Action Bar)**: Nút vàng rực **"KHỞI CHẠY GAME"** / **"CÀI ĐẶT BẢN DỊCH"** với hiệu ứng hover lift, cùng nút mở nhanh thư mục và tùy chỉnh mod.
  * **Thẻ Trạng Thái & Kiểm Tra Toàn Vẹn**: Hiển thị phiên bản game (3.9.0), phiên bản Việt hóa (v2.3.0), cùng huy hiệu bảo vệ an toàn (không can thiệp anti-cheat).
  * **Bảng Tin Tức & Cộng Đồng**: Danh sách bài viết thông báo mới nhất kèm phím tắt truy cập GitHub cộng đồng.
* **Tab 2: Quản lý Việt Hóa (Translation Patch Manager)**:
  * Hiển thị số liệu thống kê: 48,250+ câu thoại, 1,240 thuật ngữ chuẩn hóa, hoàn tất Chương 1 - 5.
  * Thao tác cài đặt / cập nhật / gỡ bỏ bản dịch với thanh tiến trình tương tác theo từng giai đoạn.
  * Các tùy chọn Mod nâng cao dạng công tắc toggle (Bật/tắt dịch cốt truyện, Dịch UI, Font chữ cổ điển Retro Serif, Giữ nguyên thuật ngữ gốc).
* **Tab 3: Tin tức & Cập nhật (News & Events)**:
  * Lưới thẻ tin tức phong phú, phân loại rõ ràng (Sự kiện, Bản vá, Hướng dẫn, Cốt truyện).
  * **Trình Đọc Bài Viết (Article Reader Modal)**: Nhấp vào bất kỳ tin tức nào để mở cửa sổ đọc bài viết chi tiết ngay trong launcher!
* **Tab 4: Nhật ký thay đổi (Changelog)**:
  * Trục thời gian (Timeline) phong cách cổ điển ghi lại chi tiết các phiên bản từ v1.0.0 đến v2.3.0.
* **Hộp Thoại Cài Đặt (Settings Window)**:
  * Thiết kế lại theo phong cách HoYoPlay với 5 phân mục: *Cài đặt chung, Đường dẫn game, Tải xuống, Công cụ & Chẩn đoán, Giới thiệu*.

### 3. Tích Hợp Native IPC (Electron Backend Mới)
* Hỗ trợ hộp thoại Windows thật để chọn thư mục cài game: `window.launcher?.selectFolder()`.
* Mở trực tiếp thư mục cài đặt trong Windows Explorer: `window.launcher?.openFolder()`.
* Tự động quét và chạy file game `Reverse1999.exe` khi nhấn "Khởi chạy game".
* Mở các liên kết ngoài an toàn bằng trình duyệt mặc định: `window.launcher?.openUrl()`.

---

## 🚀 HƯỚNG DẪN KHỞI CHẠY & ĐÓNG GÓI

### 1. Khởi chạy thử nghiệm nhanh
Nhấp đúp chuột vào file:
```
D:\Beta\R199Launc\run_launcher.bat
```
Ứng dụng launcher mới sẽ mở lên ngay lập tức!

### 2. Chế độ phát triển (Dev Mode)
Nếu muốn tiếp tục sửa đổi mã nguồn:
```powershell
cd "D:\Beta\R199Launc"
npm run dev
```

### 3. Đóng gói ra file cài đặt (.EXE) để chia sẻ cho cộng đồng
Nhấp đúp chuột vào file:
```
D:\Beta\R199Launc\run_build_installer.bat
```
Bộ cài đặt NSIS chuyên nghiệp sẽ tự động được tạo ra trong thư mục `release/`!
