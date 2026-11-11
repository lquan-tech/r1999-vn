import os
import sys
import shutil
import UnityPy
from PIL import Image

WORKSPACE = os.path.dirname(os.path.abspath(__file__))
BUNDLE_PATH = os.path.join(WORKSPACE, "7335e2dfbf8ec0e4389bc697130b63e2.dat")
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

def main():
    print("=" * 70)
    print("🎨 CÔNG CỤ THAY ĐỔI ẢNH THEME KHỞI ĐỘNG REVERSE: 1999 (VIỆT HÓA)")
    print("=" * 70)

    if not os.path.isfile(BUNDLE_PATH):
        print(f"❌ Không tìm thấy AssetBundle tại: {BUNDLE_PATH}")
        sys.exit(1)

    os.makedirs(CUSTOM_DIR, exist_ok=True)

    # Nhận diện ảnh tùy chỉnh trong thư mục custom_images/
    # Ảnh 1 (Tháp đồng hồ London / Màn hình kiểm tra cập nhật)
    candidate_anh1 = None
    for name in ["anh1.png", "anh1.jpg", "anh1.jpeg", "yumyum-bg.png", "yumyum-bg.jpg"]:
        p = os.path.join(CUSTOM_DIR, name)
        if os.path.isfile(p):
            candidate_anh1 = p
            break

    # Ảnh 2 (Banner nạp tài nguyên / Loading screen)
    candidate_anh2 = None
    for name in ["anh2.png", "anh2.jpg", "anh2.jpeg", "banner.png", "banner.jpg", "yumyum.png"]:
        p = os.path.join(CUSTOM_DIR, name)
        if os.path.isfile(p):
            candidate_anh2 = p
            break

    if not candidate_anh1 and not candidate_anh2:
        print("\nℹ️  HƯỚNG DẪN:")
        print(f"   Vui lòng đặt ảnh mới của bạn vào thư mục:\n   👉 {CUSTOM_DIR}")
        print("\n   Tên file hỗ trợ:")
        print("   - anh1.png (hoặc anh1.jpg / yumyum-bg.png): Thay thế Ảnh 1 (Tháp đồng hồ London)")
        print("   - anh2.png (hoặc anh2.jpg / banner.png):     Thay thế Ảnh 2 (Banner loading tím)")
        print("\n   Sau đó chạy lại script này: python replace_theme_images.py")
        sys.exit(0)

    print(f"\n[1/3] Đang nạp tệp bundle: {os.path.basename(BUNDLE_PATH)}...")
    with open(BUNDLE_PATH, "rb") as f:
        header = f.read(6)
        data = f.read()

    env = UnityPy.load(data)
    replaced_count = 0

    print("\n[2/3] Đang thay thế textures...")
    for obj in env.objects:
        if obj.type.name == "Texture2D":
            d = obj.read()
            name = getattr(d, 'm_Name', getattr(d, 'name', ''))

            # Thay thế Ảnh 1 (Tháp đồng hồ London)
            if candidate_anh1 and name == "yumyum-bg":
                img = Image.open(candidate_anh1)
                d.image = img
                d.save()
                replaced_count += 1
                print(f"   ✓ Đã thay thế Texture: {name} <- {os.path.basename(candidate_anh1)} ({img.size})")

            # Thay thế Ảnh 2 (Loading / Banner)
            elif candidate_anh2 and name in ("banner", "banner_sfw", "yumyum", "yumyum_v1", "yumyum_v3"):
                img = Image.open(candidate_anh2)
                d.image = img
                d.save()
                replaced_count += 1
                print(f"   ✓ Đã thay thế Texture: {name} <- {os.path.basename(candidate_anh2)} ({img.size})")

    if replaced_count == 0:
        print("⚠️ Không có texture nào được thay thế.")
        sys.exit(0)

    print(f"\n[3/3] Đang đóng gói lại AssetBundle (lz4)...")
    packed_data = env.file.save(packer="lz4")
    with open(BUNDLE_PATH, "wb") as f:
        f.write(header)
        f.write(packed_data)
    print(f"   ✓ Đã cập nhật xong bundle gốc tại: {BUNDLE_PATH}")

    # Tự động đồng bộ vào thư mục game PersistentRoot nếu có
    game_dir = find_game_path()
    if game_dir:
        game_bundle = os.path.join(game_dir, "reverse1999_Data", "StreamingAssets", "PersistentRoot", "bundles", "7335e2dfbf8ec0e4389bc697130b63e2.dat")
        if os.path.isfile(game_bundle):
            try:
                shutil.copyfile(BUNDLE_PATH, game_bundle)
                print(f"   ✓ Đã đồng bộ trực tiếp vào game: {game_bundle}")
            except Exception as e:
                print(f"   ⚠️ Thư mục game yêu cầu quyền Admin để ghi trực tiếp ({e}). Hãy bấm nút 'Cài đặt / Đồng bộ' trong viLauncher để nạp vào game!")
    
    print("\n🎉 HOÀN TẤT THAY ĐỔI ẢNH THEME THÀNH CÔNG!")
    print("=" * 70)

if __name__ == "__main__":
    main()
