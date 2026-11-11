# HƯỚNG DẪN KIẾN TRÚC TOÀN DIỆN VỀ HỆ THỐNG VIỆT HÓA REVERSE: 1999
## Tài liệu chuyển giao kỹ thuật, cơ chế tiêm ngôn ngữ, giao thức Hotupdate Bluepoch và phát triển Launcher

> **Dự án**: Reverse: 1999 Việt Hóa Cộng Đồng  
> **Kho mã nguồn GitHub**: [dat514/Reverse-1999-Vi-t-H-a](https://github.com/dat514/Reverse-1999-Vi-t-H-a)  
> **Phiên bản Game mục tiêu**: *Reverse: 1999 Global* (Client v3.9.0 / Internal 111.0.1076.0)  
> **Công nghệ Game**: Unity 2022.3.62f2/f3, il2cpp (x86_64), LuaJIT GC64, ToLua C API  
> **Công nghệ Launcher**: Electron 33, Node.js 20+, Vite 6, React 18, TypeScript, Lucide Icons  

---

## 1. TỔNG QUAN HỆ THỐNG & CÁC NHÁNH KỸ THUẬT

### 1.1. Bối cảnh & Thách thức kỹ thuật
Reverse: 1999 bảo vệ mã nguồn Lua và tài nguyên thông qua 2 lớp bảo mật:
1. **Container Lua Bytecode (`luabytes/*.dat`)**: Được đóng gói bằng cấu trúc chunk nén LZ4 có **48 byte RSA-2048 Digital Signature** ở đầu tệp. Chữ ký này xác thực tính toàn vẹn của toàn bộ nội dung container.
2. **Hệ thống Hotupdate Trực tuyến (`hotupdate-hw.sl916.com`)**: Game liên tục kiểm tra chữ ký và manifest các gói tài nguyên tải về tại thư mục `PersistentRoot`.

### 1.2. Kết luận dứt khoát về các nhánh nghiên cứu:
- **Nhánh A (Sửa trực tiếp container gốc Bluepoch)**:
  - *Kết luận*: **Bị chặn bởi mã hóa RSA**. Chữ ký RSA-2048 bao phủ toàn bộ thân container. Bất kỳ sự thay đổi dù chỉ 1 byte trong chunk Lua nào cũng làm chữ ký mất hiệu lực và game crash ngay khi khởi động (`Player.log` báo lỗi chữ ký).
- **Nhánh B (Phương án triển khai sản phẩm - Route B v5 Clean)**:
  - *Kết luận*: **Hoạt động hoàn hảo 100%**. Tận dụng container `6ac5a62c64b72b07e9383583eef5c3ac.dat` (206,342 B) có cấu trúc chữ ký hợp lệ từ thời kỳ tiền v1.9, kết hợp với AssetBundle tùy biến `7335e2dfbf8ec0e4389bc697130b63e2.dat`.
  - Trong bản v5 Clean: Đã triệt tiêu 100% rác Lerritay (xóa sạch floating widget ON/OFF, xóa chữ hồng FCK OFF), tùy biến Popup thành Bảng cảnh báo chống lừa đảo (Anti-Scam Modal) và nạp toàn bộ 115,812 chuỗi UI, 135,060 câu thoại, 189 Texture đồ họa Việt hóa.
- **Nhánh D (Launcher Cộng Đồng & Tích Hợp API Bluepoch)**:
  - Xây dựng Launcher hoàn chỉnh tại `D:\Beta\0.1.18.1\R199Launc`.
  - Tự động gọi API `hotupdate-hw.sl916.com` để theo dõi cập nhật phiên bản và đồng bộ bản dịch.

---

## 2. CƠ CHẾ ĐƯA TIẾNG VIỆT VÀO GAME (ROUTE B V5 CLEAN)

### 2.1. Bản đồ phân bổ tệp tin (Physical Footprint)
Tất cả các tệp tùy biến của bản Việt Hóa được đặt tại:
`[Thư mục Game]\reverse1999_Data\StreamingAssets\PersistentRoot\`

```
PersistentRoot/
├── luabytes/
│   └── 6ac5a62c64b72b07e9383583eef5c3ac.dat   (206,342 bytes - Container hook Lua)
├── bundles/
│   └── 7335e2dfbf8ec0e4389bc697130b63e2.dat   (~232 MB - AssetBundle chứa tài nguyên & payload)
├── 2585de5ae8274f5d0061316a8569a344            (Chứa chuỗi phiên bản: 111.0.1076.0)
└── localhash                                   (Manifest JSON băm của 24,007 tệp tài nguyên)
```

### 2.2. Chi tiết cấu trúc AssetBundle `7335e2dfbf8ec0e4389bc697130b63e2.dat`
1. **Header độc quyền**: Bắt đầu bằng 6 byte `0xC4 0x4B 0xAE 0x66 0x4C 0xE2`. Ngay sau byte thứ 6 là khối header `UnityFS` chuẩn của Unity 2022.3.62f3.
2. **TextAsset `version`**: Lưu chuỗi `"0.1.18.2"`. Khi giá trị này khớp với phiên bản máy chủ trả về, hệ thống sẽ không tự kích hoạt update box không mong muốn.
3. **TextAsset `banner` (Carrier Data)**:
   - Byte 0 đến 582,342: Ảnh JPEG chuẩn JFIF (End of Image marker `0xFF 0xD9` tại byte 582,342).
   - Byte 582,344 trở đi: Cấu trúc nhị phân `"YY"`:
     $$\text{Structure} = \text{JPEG} + \text{"YY"} + \text{IV (16 bytes)} + \text{len(block1)} + \text{block1} + \text{len(ciphertext)} + \text{ciphertext}$$
   - **Thuật toán mật mã**: AES-256-CBC.
     - Khóa (Key): `SHA256(JPEG_Bytes[:582344])`.
     - Vector khởi tạo (IV): 16 byte nhị phân tại offset `582346..582362`.
     - Bản rõ (Plaintext): Chuỗi JSON UTF-8 gồm 5 bảng dữ liệu (`l`, `s`, `p`, `c`, `i`).

### 2.3. Cấu trúc cơ sở dữ liệu dịch thuật trong JSON
| Khóa | Định dạng | Mục đích sử dụng | Số lượng mục hiện tại |
|---|---|---|---|
| `l` | `{ "id_hoặc_chuỗi_gốc": "Bản dịch Tiếng Việt" }` | Giao diện người dùng (UI), Menu sảnh, Nút bấm, Hệ thống, Bảng thông báo | **115,812** |
| `s` | `{ "chapter_id": { "line_id": ["Thoại tiếng Việt", "Tên nhân vật", "Ghi chú"] } }` | Toàn bộ cốt truyện chính, chương sự kiện, đối thoại nhân vật | **135,060** |
| `p` | `{ "poem_id": "Đoạn thơ / trích dẫn tiếng Việt" }` | Thơ dẫn truyện, lời bình lịch sử đầu màn | **367** |
| `c` | `{ "char_id": { "action_id": "Câu thoại tiếng Việt" } }` | Lồng tiếng nhân vật, tương tác khi chạm trong sảnh | **7,274** |
| `i` | `{ "original_texture_name": "redirect_path" }` | 189 Texture đồ họa chương truyện đã Việt hóa | **189** |

### 2.4. Cơ chế triệt tiêu rác & Chuyển đổi Popup (v5 Clean)
- **Triệt tiêu nút ON/OFF & Overlay**:
  - Trong các bản dịch gốc của tác giả Nga (Lerritay), có 3 UGUI Prefab: `banner_ui.prefab`, `minimized_ui.prefab`, và `church_overlay_ui.prefab`.
  - Kịch bản `deploy_route_b_full_vietnamese_v5.py` xử lý triệt để:
    1. Đặt `m_Enabled = 0` trên toàn bộ 3 component `Canvas`.
    2. Đặt `m_IsActive = False` trên toàn bộ GameObject liên quan.
    3. Đẩy toàn bộ `RectTransform` ra khỏi màn hình: `AnchoredPosition = (-9999, -9999)`, `SizeDelta = (0, 0)`.
    4. Xóa sạch mọi text trong MonoBehaviour: `m_Text = ""`, `m_Color.a = 0.0`.
- **Tùy biến Popup thành Bảng Cảnh Báo Chống Lừa Đảo (Anti-Scam Modal)**:
  - Khi game kích hoạt `BootMsgBox.show(...)`, ToLua string hook sẽ tra cứu từ điển `l`.
  - Thay thế chuỗi gốc:
    - `"Сообщение из глубин ада"` $\rightarrow$ `"CẢNH BÁO LỪA ĐẢO"`
    - `"Привет~ Доступно обновление!\n0.1.18.1 -> 0.1.18.2\nОбновить?"` $\rightarrow$ `"BẢN DỊCH TIẾNG VIỆT HOÀN TOÀN MIỄN PHÍ\nNếu bạn phải trả tiền để mua bản dịch này từ bất kỳ ai, bạn đã bị lừa đảo!\n\nGitHub chính thức: dat514/Reverse-1999-Vi-t-H-a"`
    - Hai nút `"Потом"` và `"Давай!"` $\rightarrow$ đặt thành rỗng `""` để xóa hoàn toàn chữ trên nút.
    - `"FCK OFF"` $\rightarrow$ đặt thành rỗng `""`.

---

## 3. KIẾN TRÚC LAUNCHER & GIAO THỨC BLUEPOCH HOTUPDATE API

Mã nguồn Launcher đặt tại `D:\Beta\0.1.18.1\R199Launc`.

### 3.1. Các thông số kỹ thuật của Giao thức Bluepoch (`sl916.com`)
Theo báo cáo dịch ngược `c:\Users\admin.DESKTOP-UMMS88H\Downloads\REPORT.md`:
- **Máy chủ cập nhật chính thức (Update Server)**: `https://hotupdate-hw.sl916.com`
- **Máy chủ CDN phân phối tài nguyên**: `https://upres-hw.sl916.com/60001`
- **Các tham số định danh ứng dụng**:
  - `gameId`: `60001`
  - `osType`: `2` (Hệ điều hành Windows)
  - `subChannel`: `4`
  - `versionType`: `200`
  - `channelId`: `"6001"`
  - `localhash`: `"2585de5ae8274f5d0061316a8569a344"` (mã băm của tệp danh mục tài nguyên `PersistentRoot/localhash`)

### 3.2. Thuật toán tạo chữ ký số `sign` (`0x140050b10`)
Để mỗi request gửi tới `hotupdate-hw.sl916.com` được chấp nhận, Launcher phải tính toán chữ ký số MD5:
$$\text{raw} = \text{"60001\_2\_4\_200\_6001\_"} + \text{version}$$
Ví dụ với phiên bản `111.0.1076.0`:
$$\text{raw} = \text{"60001\_2\_4\_200\_6001\_111.0.1076.0"}$$
$$\text{sign} = \text{MD5}(\text{raw}) = \text{"51f7bb8cf8da686eb022f1c8d4ce7c10"}$$

### 3.3. Các Endpoint nghiệp vụ
1. **Kiểm tra phiên bản mới (`/diff-update/version`)**:
   - `GET /diff-update/version?gameId=60001&osType=2&subChannel=4&versionType=200&channelId=6001&version={version}&sign={sign}&localhash={localhash}`
   - Trả về: Trạng thái phiên bản mới nhất từ Bluepoch.
2. **Lấy danh sách gói tài nguyên thay đổi (`/diff-update/resource`)**:
   - `GET /diff-update/resource?...`
   - Trả về: Manifest JSON chứa `abInfos` (danh sách asset bundles bị sửa đổi) và `packInfos` (các gói zip tải về).
3. **Quy trình thích ứng tự động khi Game cập nhật**:
   ```mermaid
   flowchart TD
       Start["Launcher Khởi Động"] --> CheckVer["Gọi /diff-update/version"]
       CheckVer --> IsDiff{"Có phiên bản mới?"}
       IsDiff -- Không --> Ready["Bản Việt Hóa hoàn toàn tương thích!"]
       IsDiff -- Có --> FetchManifest["Gọi /diff-update/resource"]
       FetchManifest --> DownloadVanilla["Tải các bundle bị đổi từ upres-hw.sl916.com"]
       DownloadVanilla --> ExtractText["Trích xuất chuỗi văn bản tiếng Anh gốc"]
       ExtractText --> AutoTranslate["Áp dụng từ điển tiếng Việt qua deploy_route_b_v5.py"]
       AutoTranslate --> Repack7335["Đóng gói lại 7335e2df...dat"]
       Repack7335 --> Ready
   ```

---

## 4. HƯỚNG DẪN DÀNH CHO AI TIẾP THEO ĐỂ TIẾP TỤC DỰ ÁN

Nếu bạn là một trợ lý AI kế nhiệm phụ trách dự án này, hãy tuân theo các chỉ dẫn chuẩn sau:

### 4.1. Lệnh thực thi cốt lõi (1-Click Run)
- **Triển khai bản Việt Hóa v5 Clean vào game**:
  ```powershell
  python D:\Beta\0.1.18.1\deploy_route_b_full_vietnamese_v5.py
  ```
  Kịch bản này tự động thực hiện 4 bước: Nạp từ điển $\rightarrow$ Mã hóa AES-256 $\rightarrow$ Xóa sạch rác/widget $\rightarrow$ Ghi file trực tiếp vào thư mục cài đặt game `PersistentRoot`.

- **Biên dịch Launcher mã nguồn mở**:
  ```powershell
  cd D:\Beta\0.1.18.1\R199Launc
  npm run build
  ```
  Lệnh này sẽ kiểm tra TypeScript (`tsc --noEmit`), build React qua Vite và đóng gói Electron qua `esbuild`. Kết quả nằm tại `dist/` và `dist-electron/`.

- **Chạy Launcher ở chế độ phát triển (Dev Mode)**:
  ```powershell
  cd D:\Beta\0.1.18.1\R199Launc
  npm run dev
  ```

### 4.2. Cách bổ sung thuật ngữ & sửa lỗi dịch (Proofreading Workflow)
1. Mở tệp `D:\Beta\0.1.18.1\deploy_route_b_full_vietnamese_v5.py`.
2. Tìm mảng `SPECIAL_OVERRIDE_STRINGS` hoặc `CORRECTIONS`:
   - Muốn sửa giao diện cụ thể: Thêm cặp `("Chuỗi gốc hoặc tiếng Nga", "Bản dịch Tiếng Việt chuẩn")`.
   - Muốn sửa tên nhân vật: Cập nhật trong `NAMES_MAP`.
3. Chạy lại `python D:\Beta\0.1.18.1\deploy_route_b_full_vietnamese_v5.py`.
4. Không cần khởi động lại máy, chỉ cần mở lại game để kiểm tra kết quả ngay lập tức!
