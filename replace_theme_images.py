import os
import sys
import shutil
import argparse
import hashlib
import json
import struct
from Crypto.Cipher import AES
import UnityPy
from PIL import Image

WORKSPACE = os.path.dirname(os.path.abspath(__file__))
BUNDLE_PATH = os.path.join(WORKSPACE, "7335e2dfbf8ec0e4389bc697130b63e2.dat")
BANNER_PATH = os.path.join(WORKSPACE, "custom_vietnamese_banner.bytes")
CUSTOM_DIR = os.path.join(WORKSPACE, "custom_images")

GAME_PATHS = [
    r"C:\Program Files (x86)\reverse1999_global\Reverse1999en",
    r"C:\Program Files\reverse1999_global\Reverse1999en",
    r"D:\bluepoch\reverse1999_global\Reverse1999en",
]

def find_game_path():
    for p in GAME_PATHS:
        if os.path.isfile(os.path.join(p, "reverse1999.exe")):
            return p
    return None

def resolve_image_path(name_or_path):
    if not name_or_path:
        return None
    if os.path.isfile(name_or_path):
        return os.path.abspath(name_or_path)
    cand = os.path.join(CUSTOM_DIR, name_or_path)
    if os.path.isfile(cand):
        return cand
    return None

def update_carrier_if_needed():
    """Đảm bảo carrier banner.bytes luôn khớp với client_version và không còn chữ Nga / FCK OFF."""
    if not os.path.isfile(BANNER_PATH):
        return None

    try:
        with open(BANNER_PATH, "rb") as f:
            raw = f.read()

        jpeg = raw[:582344]
        key = hashlib.sha256(jpeg).digest()
        iv = raw[582346:582346+16]
        b1_len = int.from_bytes(raw[582362:582366], 'little')
        block1 = raw[582366 : 582366 + b1_len]

        b2_offset = 582366 + b1_len
        b2_len = int.from_bytes(raw[b2_offset:b2_offset+4], 'little')
        ct = raw[b2_offset+4 : b2_offset+4+b2_len]

        cipher = AES.new(key, AES.MODE_CBC, iv)
        pt = cipher.decrypt(ct)
        pad = pt[-1]
        pt = pt[:-pad]
        data = json.loads(pt.decode('utf-8'))

        # Tìm version thực tế của client game nếu có
        game_path = find_game_path()
        client_version = "111.0.1077"
        if game_path:
            v_file = os.path.join(game_path, "reverse1999_Data", "StreamingAssets", "PersistentRoot", "2585de5ae8274f5d0061316a8569a344")
            if os.path.isfile(v_file):
                with open(v_file, "r", encoding="utf-8", errors="ignore") as vf:
                    raw_v = vf.read().strip()
                    parts = raw_v.split('.')
                    if len(parts) >= 3:
                        client_version = f"{parts[0]}.{parts[1]}.{parts[2]}"

        data["gameVersion"] = client_version

        # Bổ sung các chuỗi tiếng Nga / cảnh báo còn sót vào từ điển 'l'
        overrides = {
            "Игра новее перевода — часть текста может быть на английском~": "",
            "Игра новее перевода — часть текста может быть на английском": "",
            "Игра новее перевода": "",
            f"Перевод: 0.1.18.2 | Игра: {client_version}": "Bản dịch Tiếng Việt | Reverse: 1999 Global",
            "Перевод: 0.1.18.2 | Игра: 111.0.1076": "Bản dịch Tiếng Việt | Reverse: 1999 Global",
            "Перевод: 0.1.18.2 | Игра: 111.0.1077": "Bản dịch Tiếng Việt | Reverse: 1999 Global",
            "Перевод: 0.1.18.2 | Игра: 111.0.1078": "Bản dịch Tiếng Việt | Reverse: 1999 Global",
            "FCK OFF": "",
            "FCK": "",
            "Палимпсест": "",
            "Восстановить": "Khôi phục",
            "Загрузка ресурсов, подожди~...": "Đang tải tài nguyên, vui lòng chờ~...",
            "Загрузка ресурсов, подожди~": "Đang tải tài nguyên, vui lòng chờ~",
            "Загрузка ресурсов...": "Đang tải tài nguyên...",
            "Загрузка ресурсов": "Đang tải tài nguyên",
            "Проверяю обновления~...": "Đang kiểm tra cập nhật~...",
            "Проверяю обновления~": "Đang kiểm tra cập nhật~",
            "Проверяю обновления...": "Đang kiểm tra cập nhật...",
            "Проверяю обновления": "Đang kiểm tra cập nhật",
            "Рукопись, где старый текст соскоблили, чтобы записать новый. Прежние строки всегда проступают под свежими. В архиве всё наоборот: под оригиналом проступает перевод — будто он был там раньше. Библиотекарь клянётся, что такие страницы пахнут вишнёвым ликёром.": "",
        }

        for k, v in overrides.items():
            data["l"][k] = v

        new_json = json.dumps(data, ensure_ascii=False, separators=(',', ':')).encode('utf-8')
        bs = 16
        pad_need = bs - (len(new_json) % bs)
        padded = new_json + bytes([pad_need] * pad_need)
        cipher_enc = AES.new(key, AES.MODE_CBC, iv)
        new_ct = cipher_enc.encrypt(padded)

        new_banner_bytes = (
            jpeg +
            b'YY' +
            iv +
            struct.pack('<I', len(block1)) +
            block1 +
            struct.pack('<I', len(new_ct)) +
            new_ct
        )

        with open(BANNER_PATH, "wb") as f:
            f.write(new_banner_bytes)

        return new_banner_bytes
    except Exception as e:
        print(f"⚠️ Không thể cập nhật carrier banner: {e}")
        return None

def main():
    parser = argparse.ArgumentParser(description="Thay thế ảnh theme khởi động và loading Reverse: 1999")
    parser.add_argument("--splash", "-s", help="Tên hoặc đường dẫn ảnh thay thế cho Splash screen (yumyum-bg)")
    parser.add_argument("--loading", "-l", help="Tên hoặc đường dẫn ảnh thay thế cho Loading banner (yumyum, yumyum_v4, banner...)")
    args = parser.parse_args()

    print("=" * 70)
    print("🎨 CÔNG CỤ TÙY BIẾN THEME & SẠCH CHỮ NGA REVERSE: 1999 (VIỆT HÓA)")
    print("=" * 70)

    if not os.path.isfile(BUNDLE_PATH):
        print(f"❌ Không tìm thấy AssetBundle tại: {BUNDLE_PATH}")
        sys.exit(1)

    os.makedirs(CUSTOM_DIR, exist_ok=True)

    candidate_anh1 = resolve_image_path(args.splash)
    candidate_anh2 = resolve_image_path(args.loading)

    # Nếu không có tham số CLI, tìm ảnh tốt nhất trong custom_images
    if not candidate_anh1:
        for name in ["image (2).jpg", "anh1.png", "anh1.jpg", "yumyum-bg.png", "yumyum-bg.jpg"]:
            p = os.path.join(CUSTOM_DIR, name)
            if os.path.isfile(p):
                candidate_anh1 = p
                break

    if not candidate_anh2:
        for name in ["f1bd0134-0feb-4add-a6b0-3b1a18b0b0f8.png", "anh2.png", "anh2.jpg", "banner.png", "yumyum.png"]:
            p = os.path.join(CUSTOM_DIR, name)
            if os.path.isfile(p):
                candidate_anh2 = p
                break

    print(f"\n[1/3] Đang cập nhật carrier bản dịch và nạp bundle: {os.path.basename(BUNDLE_PATH)}...")
    new_banner_bytes = update_carrier_if_needed()

    with open(BUNDLE_PATH, "rb") as f:
        header = f.read(6)
        data = f.read()

    env = UnityPy.load(data)
    replaced_count = 0

    print("\n[2/3] Đang thay thế textures và cập nhật carrier TextAsset...")
    for obj in env.objects:
        if obj.type.name == "TextAsset":
            d = obj.read()
            name = getattr(d, 'm_Name', getattr(d, 'name', ''))
            if name == "banner" and new_banner_bytes:
                tree = obj.read_typetree()
                tree["m_Script"] = bytes(new_banner_bytes).decode("utf8", "surrogateescape")
                obj.save_typetree(tree)
                print(f"   ✓ [TextAsset] Đã nạp carrier mới (đồng bộ gameVersion, xóa FCK OFF & chữ Nga)")

        elif obj.type.name == "Texture2D":
            d = obj.read()
            name = getattr(d, 'm_Name', getattr(d, 'name', ''))

            # Thay thế Splash (yumyum-bg)
            if candidate_anh1 and name == "yumyum-bg":
                img = Image.open(candidate_anh1).convert("RGBA")
                d.image = img
                d.save()
                replaced_count += 1
                print(f"   ✓ Đã thay thế Texture Splash: {name} <- {os.path.basename(candidate_anh1)} ({img.size})")

            # Thay thế Loading (Toàn bộ họ yumyum và banner bao gồm cả yumyum_v4)
            elif candidate_anh2 and name in (
                "banner", "banner_sfw", "banner_3.4", "banner_3.5",
                "yumyum", "yumyum_v1", "yumyum_v3", "yumyum_v4"
            ):
                img = Image.open(candidate_anh2).convert("RGBA")
                d.image = img
                d.save()
                replaced_count += 1
                print(f"   ✓ Đã thay thế Texture Loading: {name} <- {os.path.basename(candidate_anh2)} ({img.size})")

    print(f"\n[3/3] Đang đóng gói lại AssetBundle (lz4)...")
    packed_data = env.file.save(packer="lz4")
    with open(BUNDLE_PATH, "wb") as f:
        f.write(header)
        f.write(packed_data)
    print(f"   ✓ Đã cập nhật xong bundle nguồn: {BUNDLE_PATH} ({os.path.getsize(BUNDLE_PATH):,} bytes)")

    # Tự động đồng bộ vào thư mục game PersistentRoot nếu có
    game_dir = find_game_path()
    if game_dir:
        game_bundle = os.path.join(game_dir, "reverse1999_Data", "StreamingAssets", "PersistentRoot", "bundles", "7335e2dfbf8ec0e4389bc697130b63e2.dat")
        if os.path.isfile(game_bundle):
            try:
                shutil.copyfile(BUNDLE_PATH, game_bundle)
                print(f"   ✓ Đã đồng bộ trực tiếp vào game: {game_bundle}")
            except PermissionError:
                print(f"   ℹ️ Program Files yêu cầu quyền Admin, đang gọi xác nhận UAC...")
                import subprocess
                ps_cmd = f'Copy-Item -Path "{BUNDLE_PATH}" -Destination "{game_bundle}" -Force'
                ret = subprocess.run(["powershell", "-Command", f'Start-Process powershell -Verb RunAs -ArgumentList \'-NoProfile\', \'-Command\', \'"{ps_cmd}"\' -Wait'])
                if ret.returncode == 0:
                    print("   ✓ Đã đồng bộ trực tiếp vào game thành công qua quyền Admin!")
                else:
                    print("   ⚠️ Không thể sao chép tự động. Hãy bấm 'Cài đặt / Đồng bộ' trong Launcher.")
    
    print("\n🎉 HOÀN TẤT THAY ĐỔI ẢNH THEME & DỌN DẸP SẠCH CHỮ NGA THÀNH CÔNG!")
    print("=" * 70)

if __name__ == "__main__":
    main()
