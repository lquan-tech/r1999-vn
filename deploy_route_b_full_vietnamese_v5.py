import os
import sys
import json
import re
import hashlib
import struct
import shutil
from Crypto.Cipher import AES
import UnityPy
from PIL import Image

sys.stdout.reconfigure(encoding='utf-8')

print("="*80)
print("🚀 TRIỂN KHAI PHƯƠNG ÁN B V5: XÓA SẠCH NÚT ON/OFF, FCK OFF & ANTI-SCAM MODAL")
print("="*80)

# 1. PATHS
# WORKSPACE mặc định = thư mục chứa script này (ghi đè bằng biến môi trường R1999_WORKSPACE).
# Thư mục game: tham số dòng lệnh thứ nhất, hoặc biến môi trường R1999_GAME_PATH,
# hoặc vị trí cài đặt mặc định đầu tiên tìm thấy.
WORKSPACE = os.environ.get("R1999_WORKSPACE") or os.path.dirname(os.path.abspath(__file__))
PAYLOAD_PATH = os.path.join(WORKSPACE, "DECRYPTED_RUSSIAN_PAYLOAD.bin")
P0_PATH = os.path.join(WORKSPACE, "M27_Intermediate_Export", "LocalizationData", "p0_vietnamese_data.json")
VOCAB_PATH = os.path.join(WORKSPACE, "russian_vietnamese_vocab.json")
BACKUP_7335 = os.path.join(WORKSPACE, "LERRITAY_ORIGINAL_7335_BACKUP.dat")
BANNER_SRC_PATH = os.path.join(WORKSPACE, "extracted_banner_textasset.bin")

GAME_PATH_CANDIDATES = [
    r"C:\Program Files (x86)\reverse1999_global\Reverse1999en",
    r"C:\Program Files\reverse1999_global\Reverse1999en",
    r"D:\bluepoch\reverse1999_global\Reverse1999en",
    r"D:\reverse1999_global\Reverse1999en",
]

def resolve_game_path():
    explicit = (sys.argv[1] if len(sys.argv) > 1 else None) or os.environ.get("R1999_GAME_PATH")
    if explicit:
        return explicit
    for cand in GAME_PATH_CANDIDATES:
        if os.path.isfile(os.path.join(cand, "reverse1999.exe")):
            return cand
    return None

GAME_PATH = resolve_game_path()
TARGET_7335 = os.path.join(
    GAME_PATH or "", "reverse1999_Data", "StreamingAssets", "PersistentRoot", "bundles",
    "7335e2dfbf8ec0e4389bc697130b63e2.dat",
)
TARGET_6AC5 = os.path.join(
    GAME_PATH or "", "reverse1999_Data", "StreamingAssets", "PersistentRoot", "luabytes",
    "6ac5a62c64b72b07e9383583eef5c3ac.dat",
)

PREBUILT_7335 = os.path.join(WORKSPACE, "7335e2dfbf8ec0e4389bc697130b63e2.dat")
PREBUILT_6AC5 = os.path.join(WORKSPACE, "6ac5a62c64b72b07e9383583eef5c3ac.dat")
BACKUP_DIR = os.path.join(
    os.environ.get("APPDATA") or os.path.expanduser("~"),
    "reverse-1999-viet-hoa-launcher", "vanilla_backup"
)

def test_writable(dir_path):
    probe = os.path.join(dir_path, f".write_test_{os.getpid()}")
    try:
        with open(probe, "w") as f: f.write("test")
        os.remove(probe)
        return True
    except Exception:
        return False

# Xử lý lệnh Rollback (Khôi phục file gốc)
if "--rollback" in sys.argv or "--restore" in sys.argv:
    print("\n[ROLLBACK] Đang khôi phục tệp game gốc...")
    if not GAME_PATH:
        print("❌ Không xác định được thư mục game để khôi phục.")
        sys.exit(1)
    bak_7335 = os.path.join(BACKUP_DIR, "7335e2dfbf8ec0e4389bc697130b63e2.dat")
    bak_6ac5 = os.path.join(BACKUP_DIR, "6ac5a62c64b72b07e9383583eef5c3ac.dat")
    if os.path.isfile(bak_7335) and os.path.isfile(bak_6ac5):
        try:
            shutil.copyfile(bak_7335, TARGET_7335)
            shutil.copyfile(bak_6ac5, TARGET_6AC5)
            print("  ✓ Đã khôi phục thành công các tệp gốc từ thư mục sao lưu!")
            sys.exit(0)
        except PermissionError:
            print("❌ Lỗi quyền ghi: Hãy chạy lại terminal dưới quyền 'Run as administrator'.")
            sys.exit(1)
    else:
        print("❌ Không tìm thấy bản sao lưu tệp gốc trong:", BACKUP_DIR)
        sys.exit(1)

# Kiểm tra thư mục game
if not GAME_PATH or not os.path.isdir(os.path.dirname(TARGET_7335)):
    print(f"\n❌ Không tìm thấy thư mục cài đặt game hợp lệ.")
    print(f"   Đã kiểm tra: {', '.join(GAME_PATH_CANDIDATES)}")
    print("   Cách dùng: python deploy_route_b_full_vietnamese_v5.py [đường_dẫn_thư_mục_game]")
    sys.exit(1)

# Chế độ Fast Deploy: Dùng trực tiếp gói prebuilt v5 Clean nếu không yêu cầu --rebuild
do_rebuild = "--rebuild" in sys.argv
has_prebuilt = os.path.isfile(PREBUILT_7335) and os.path.isfile(PREBUILT_6AC5)

if has_prebuilt and not do_rebuild:
    print("\n⚡ [FAST DEPLOY] Phát hiện gói tài nguyên v5 Clean đã đóng gói hoàn chỉnh:")
    print(f"   - Bundle:   {PREBUILT_7335} ({os.path.getsize(PREBUILT_7335):,} bytes)")
    print(f"   - LuaBytes: {PREBUILT_6AC5} ({os.path.getsize(PREBUILT_6AC5):,} bytes)")
    print(f"   - Mục tiêu: {GAME_PATH}")
    
    # Kiểm tra quyền ghi trước khi copy
    p_root = os.path.join(GAME_PATH, "reverse1999_Data", "StreamingAssets", "PersistentRoot")
    if not test_writable(p_root):
        print("\n❌ CẢNH BÁO QUYỀN GHI:")
        print("   Thư mục game nằm trong phân vùng được Windows bảo vệ (Program Files).")
        print("   Vui lòng mở PowerShell hoặc Command Prompt bằng quyền 'Run as administrator' rồi chạy lại script.")
        sys.exit(1)
        
    os.makedirs(os.path.dirname(TARGET_7335), exist_ok=True)
    os.makedirs(os.path.dirname(TARGET_6AC5), exist_ok=True)
    
    # Sao lưu file gốc nếu chưa có bản sao lưu
    os.makedirs(BACKUP_DIR, exist_ok=True)
    bak_7335 = os.path.join(BACKUP_DIR, "7335e2dfbf8ec0e4389bc697130b63e2.dat")
    bak_6ac5 = os.path.join(BACKUP_DIR, "6ac5a62c64b72b07e9383583eef5c3ac.dat")
    if not os.path.isfile(bak_7335) and os.path.isfile(TARGET_7335):
        print("  ✓ Đang sao lưu bundle gốc vào:", bak_7335)
        shutil.copyfile(TARGET_7335, bak_7335)
    if not os.path.isfile(bak_6ac5) and os.path.isfile(TARGET_6AC5):
        print("  ✓ Đang sao lưu container Lua gốc vào:", bak_6ac5)
        shutil.copyfile(TARGET_6AC5, bak_6ac5)
        
    # Triển khai gói mod
    print("  🚀 Đang sao chép các tệp mod vào game...")
    shutil.copyfile(PREBUILT_7335, TARGET_7335)
    shutil.copyfile(PREBUILT_6AC5, TARGET_6AC5)
    print(f"  ✓ Đã ghi đè bundle 7335: {TARGET_7335}")
    print(f"  ✓ Đã ghi đè container Lua 6ac5: {TARGET_6AC5}")
    print("\n🎉 TRIỂN KHAI HOÀN TẤT: KHÔNG CÒN NÚT ON/OFF, KHÔNG CÒN FCK OFF, BẢNG CẢNH BÁO ĐÃ SẴN SÀNG!")
    print("="*80)
    sys.exit(0)

# Chế độ Full Rebuild: Kiểm tra các tệp đầu vào thô
_missing = [p for p in (PAYLOAD_PATH, P0_PATH, VOCAB_PATH, BACKUP_7335, BANNER_SRC_PATH) if not os.path.isfile(p)]
if _missing:
    print(f"\n❌ Chế độ biên dịch lại (--rebuild) yêu cầu đầy đủ các tệp dữ liệu thô (WORKSPACE = {WORKSPACE}):")
    for p in _missing:
        print(f"   - {p}")
    print("\nGợi ý: Để triển khai trực tiếp gói v5 Clean đã đóng gói, hãy chạy script không có cờ --rebuild.")
    sys.exit(1)
print(f"WORKSPACE : {WORKSPACE}")
print(f"GAME_PATH : {GAME_PATH}")

USER_REPO = "https://github.com/dat514/Reverse-1999-Vi-t-H-a"

# 2. TỪ ĐIỂN CỤM TỪ ANTI-SCAM, LOADING & SCREENSHOT
SPECIAL_OVERRIDE_STRINGS = [
    # Bảng thông báo / Anti-scam modal
    ("Сообщение из глубин ада", "CẢNH BÁO LỪA ĐẢO"),
    ("Привет~ Доступно обновление!\n0.1.18.1 -> 0.1.18.2\nОбновить?", "BẢN DỊCH TIẾNG VIỆT HOÀN TOÀN MIỄN PHÍ\nNếu bạn phải trả tiền để mua bản dịch này từ bất kỳ ai, bạn đã bị lừa đảo!\n\nGitHub chính thức: dat514/Reverse-1999-Vi-t-H-a"),
    ("Привет~ Доступно обновление!", "BẢN DỊCH TIẾNG VIỆT HOÀN TOÀN MIỄN PHÍ"),
    ("Обновить?", ""),
    ("Потом", ""),
    ("Давай!", ""),
    ("Давай", ""),
    ("Проверяю обновления~", "Đang kiểm tra cập nhật~"),
    ("Проверяю обновления", "Đang kiểm tra cập nhật"),
    ("Уведомление", "THÔNG BÁO QUAN TRỌNG"),
    
    # Rác màn hình chờ & loading
    ("FCK OFF", ""),
    ("FCK", ""),
    ("Перевод:", "Bản dịch:"),
    ("Перевод: 0.1.18.1 | Игра: 111.0.1076", "Bản dịch Tiếng Việt | Reverse: 1999 Global"),
    ("Перевод: 0.1.18.2 | Игра: 111.0.1076", "Bản dịch Tiếng Việt | Reverse: 1999 Global"),
    ("Загрузка ресурсов, подожди~...", "Đang tải tài nguyên, vui lòng chờ~..."),
    ("Загрузка ресурсов, подожди~", "Đang tải tài nguyên, vui lòng chờ~"),
    ("Загрузка ресурсов", "Đang Tải Tài Nguyên"),
    
    # Text sót từ screenshot của user
    ("Унилог", "Unilog"),
    ("Ода огню и морю", "Khúc Ca Lửa Và Biển"),
    ("Красное и Белое", "Đỏ Và Trắng"),
    ("Победные Приливы", "Thủy Triều Thắng Lợi"),
    ("Новые Чудеса", "Kỳ Tích Mới"),
    ("Новых Чудес", "Kỳ Tích Mới"),
    ("Чудо-Маркет", "Chợ Kỳ Diệu"),
    ("Чудо-Маркета", "Chợ Kỳ Diệu"),
    ("Ломбард · Тембр-лавка", "Tiệm Cầm Đồ · Quán Âm Sắc"),
    ("Ломбард", "Tiệm Cầm Đồ"),
    ("Тембр-лавка", "Quán Âm Sắc"),
    ("Эхо Дождя", "Tiếng Vọng Cơn Mưa"),
    ("Возвращение к Наградам", "Trở Về Nhận Thưởng"),
    ("внутреннее убранство", "bên trong"),
    ("местонахождение", "vị trí"),
    ("ожерелья", "dây chuyền"),
    ("драгоценным", "quý giá"),
    ("подключения:", "đăng nhập:"),
    ("подключения", "đăng nhập"),
    ("Ngày подключения:", "Số ngày đăng nhập:"),
    ("ĐẾN may mắn thay, cái chết", "Thật may mắn, cái chết"),
    ("Cục đá", "Đá Quý"),
    ("và rất khá", "và rất có thể"),
    ("chính ban. khi nào thấy. cái gì kết", "chính bạn khi nào thấy được kết cục"),
    ("xac định", "Xác định"),
    ("được cho là nguyền rủa", "được cho là lời nguyền của"),
    ("Ревущий Месяц", "Tháng Gầm Vang"),
    ("Машина Рассказчика", "Cỗ Máy Kể Chuyện"),
    ("Стремление Снега", "Khát Vọng Tuyết Trắng"),
    ("Окно в Другие Миры", "Cửa Sổ Sang Thế Giới Khác"),
    ("Глушь", "Vùng Hoang Dã"),
    ("Глуши", "Vùng Hoang Dã"),
    ("Лимб", "Vực Sâu Limbo"),
    ("Лимбо", "Vực Sâu Limbo"),
    ("В Лимбо", "Vào Vực Sâu Limbo"),
    ("В сети", "Đang Hoạt Động"),
    ("Не в сети", "Ngoại Tuyến"),
    ("Дней в пути", "Số Ngày Hành Trình"),
    ("Дата отбытия", "Ngày Khởi Hành"),
    ("Достижения", "Thành Tựu"),
    ("Достижений", "Thành Tựu"),
    ("Коллекция", "Bộ Sưu Tập"),
    ("Девиз", "Châm Ngôn"),
    ("Введите ваш девиз", "Nhập châm ngôn của bạn"),
    ("В какой сцене ты находишься?", "Bạn đang ở phân cảnh nào?"),
    ("3RD-2 Лягушка и Ириска", "3RD-2 Ếch Và Kẹo Bơ Cứng"),
    ("Лягушка и Ириска", "Ếch Và Kẹo Bơ Cứng"),
    ("Хранительница Времени", "Người Gìn Giữ Thời Gian"),
    ("Хранитель времени", "Người Gìn Giữ Thời Gian"),
    ("Фонд Святого Павлова", "Tổ Chức Thánh Pavlov"),
    ("Манус Вендиката", "Manus Vindicta"),
    ("«Шторм»", "«Cơn Bão»"),
    ("Шторм", "Cơn Bão"),
    ("арканист", "pháp sư huyền bí"),
    ("Чистые Капли", "Giọt Tinh Khiết"),
    ("Чистых Капель", "Giọt Tinh Khiết"),
    ("Кристальные Капли", "Giọt Tinh Thể"),
]

# Sắp xếp theo chiều dài giảm dần
SPECIAL_OVERRIDE_STRINGS.sort(key=lambda x: len(x[0]), reverse=True)

# 3. TÊN NHÂN VẬT & BIẾN THỂ CÁCH CHIA
NAMES_MAP = {
    "Вертин": "Vertin", "Вертина": "Vertin", "Вертину": "Vertin", "Вертином": "Vertin", "Вертине": "Vertin",
    "Сонетто": "Sonetto",
    "Регулус": "Regulus", "Регулуса": "Regulus", "Регулусу": "Regulus", "Регулусом": "Regulus", "Регулусе": "Regulus",
    "Семмельвайс": "Semmelweis", "Семмельвайса": "Semmelweis",
    "Матильда": "Matilda", "Матильды": "Matilda", "Матильде": "Matilda", "Матильду": "Matilda",
    "Лиля": "Lilya", "Лили": "Lilya", "Лиле": "Lilya", "Лилю": "Lilya",
    "Сотби": "Sotheby",
    "Какания": "Kakania", "Какании": "Kakania", "Каканию": "Kakania", "Каканией": "Kakania",
    "Виндсонг": "Windsong", "Виндсонга": "Windsong", "Виндсонгу": "Windsong",
    "Икс": "X",
    "Тридцать семь": "37", "тридцать семь": "37", "Шесть": "6", "шесть": "6",
    "Энигма": "Enigma", "Энигмы": "Enigma", "Энигме": "Enigma",
    "Маркус": "Marcus", "Маркуса": "Marcus", "Маркусу": "Marcus", "Маркусом": "Marcus",
    "Люси": "Lucy",
    "Лян Ю": "Liang Yue",
    "Реколета": "Recoleta",
}

# 4. LOAD VOCABULARY & STEM MAP
with open(VOCAB_PATH, "r", encoding="utf-8") as f:
    auto_vocab = json.load(f)

CORRECTIONS = {
    "унилог": "Unilog", "унилога": "Unilog", "унилоги": "Unilog",
    "призыв": "chiêu mộ", "опыт": "kinh nghiệm", "награда": "phần thưởng",
    "задание": "nhiệm vụ", "событие": "sự kiện", "история": "cốt truyện",
    "бой": "chiến đấu", "победа": "chiến thắng", "поражение": "thất bại",
    "заклинание": "thuật thức", "ультимейт": "nghi thức tối thượng",
    "подтвердить": "xác nhận", "отмена": "hủy bỏ", "закрыть": "đóng", "назад": "quay lại",
    "вкл": "Bật", "выкл": "Tắt"
}
auto_vocab.update(CORRECTIONS)

stem_map = {}
for w, tr in auto_vocab.items():
    if len(w) > 3:
        for cut in [1, 2, 3]:
            st = w[:-cut]
            if len(st) >= 3 and st not in stem_map:
                stem_map[st] = tr

def translate_str(text):
    if not text or not isinstance(text, str):
        return text
    
    # 1. Screenshot & UI phrases (Exact string match theo thứ tự dài nhất trước)
    for ru, vi in SPECIAL_OVERRIDE_STRINGS:
        if ru in text:
            text = text.replace(ru, vi)
            
    # 2. Names map (Word boundary match)
    for ru, vi in NAMES_MAP.items():
        if ru in text:
            text = re.sub(r'\b' + re.escape(ru) + r'\b', vi, text)
            
    # 3. Morphological stem & vocab replacement for remaining Cyrillic words
    def repl(m):
        w = m.group(0)
        lw = w.lower()
        if lw in auto_vocab:
            tr = auto_vocab[lw]
        else:
            tr = None
            for cut in [1, 2, 3, 4]:
                if len(lw) > cut + 2:
                    st = lw[:-cut]
                    if st in stem_map:
                        tr = stem_map[st]
                        break
            if not tr:
                return w
        if w.isupper(): return tr.upper()
        if w[0].isupper(): return tr.capitalize()
        return tr

    return re.sub(r'[\u0400-\u04ff]+', repl, text)

# ==============================================================================
# BƯỚC 1: NẠP & DỊCH TOÀN BỘ CƠ SỞ DỮ LIỆU
# ==============================================================================
print("\n[BƯỚC 1/4] Nạp và Việt Hóa toàn diện 4 bảng dữ liệu (l, s, p, c)...")
with open(PAYLOAD_PATH, "rb") as f:
    raw = f.read()

pad_len = raw[-1]
raw_unpad = raw[:-pad_len] if pad_len <= 16 else raw
data = json.loads(raw_unpad.decode('utf-8'))

# Nạp 5,371 chuỗi P0 chính thức
with open(P0_PATH, "r", encoding="utf-8") as f:
    p0 = json.load(f)

for item in p0:
    iid = item.get("id")
    tvi = item.get("target_vi")
    if iid and tvi:
        data["l"][iid] = tvi

# Đưa trực tiếp các cụm từ override vào l
for ru, vi in SPECIAL_OVERRIDE_STRINGS:
    data["l"][ru] = vi

# Dịch toàn diện các chuỗi 'l'
l_count = 0
for k, v in data["l"].items():
    if isinstance(v, str):
        new_v = translate_str(v)
        if new_v != v:
            data["l"][k] = new_v
            l_count += 1

# Dịch các cảnh đối thoại 's'
s_count = 0
for sc_id, sc in data["s"].items():
    if isinstance(sc, dict):
        for line_id, line in sc.items():
            if isinstance(line, list):
                if len(line) > 0 and isinstance(line[0], str):
                    new_txt = translate_str(line[0])
                    if new_txt != line[0]:
                        line[0] = new_txt
                        s_count += 1
                if len(line) > 1 and isinstance(line[1], str):
                    sp = line[1]
                    if sp in NAMES_MAP: line[1] = NAMES_MAP[sp]
                    else: line[1] = translate_str(line[1])
                if len(line) > 2 and isinstance(line[2], str):
                    line[2] = translate_str(line[2])
                if len(line) > 4 and isinstance(line[4], list):
                    for ch in line[4]:
                        if isinstance(ch, list) and len(ch) > 1 and isinstance(ch[1], str):
                            ch[1] = translate_str(ch[1])

# Dịch thơ dẫn truyện 'p' và lồng tiếng 'c'
p_count = 0
for pk, pv in data["p"].items():
    if isinstance(pv, str):
        data["p"][pk] = translate_str(pv)
        p_count += 1

c_count = 0
for ck, cv in data["c"].items():
    if isinstance(cv, dict):
        for vk, vv in cv.items():
            if isinstance(vv, str):
                cv[vk] = translate_str(vv)
                c_count += 1

data["url"] = USER_REPO
data["translationVersion"] = "0.1.18.2"
data["gameVersion"] = "111.0.1076"

print(f"  ✓ Hoàn tất Việt hóa: l={l_count:,}, s={s_count:,}, p={p_count:,}, c={c_count:,}")

# ==============================================================================
# BƯỚC 2: MÃ HÓA AES-256-CBC VÀ ĐÓNG GÓI BANNER.BYTES
# ==============================================================================
print("\n[BƯỚC 2/4] Mã hóa AES-256-CBC và đóng gói banner.bytes...")
with open(BANNER_SRC_PATH, "rb") as f:
    banner_raw = f.read()

jpeg = banner_raw[:582344]
key = hashlib.sha256(jpeg).digest()
iv = banner_raw[582346:582346+16]
b1_len = int.from_bytes(banner_raw[582362:582366], 'little')
block1 = banner_raw[582366 : 582366 + b1_len]

new_json_bytes = json.dumps(data, ensure_ascii=False, separators=(',', ':')).encode('utf-8')
print(f"  ✓ Kích thước JSON Tiếng Việt: {len(new_json_bytes):,} bytes")

bs = 16
pad_need = bs - (len(new_json_bytes) % bs)
padded = new_json_bytes + bytes([pad_need] * pad_need)

cipher = AES.new(key, AES.MODE_CBC, iv)
new_ct = cipher.encrypt(padded)

new_banner = (
    jpeg +
    b'YY' +
    iv +
    struct.pack('<I', len(block1)) +
    block1 +
    struct.pack('<I', len(new_ct)) +
    new_ct
)

OUT_BANNER_PATH = os.path.join(WORKSPACE, "custom_vietnamese_banner.bytes")
with open(OUT_BANNER_PATH, "wb") as f:
    f.write(new_banner)
print(f"  ✓ Đã lưu banner.bytes: {len(new_banner):,} bytes")

# ==============================================================================
# BƯỚC 3: XỬ LÝ ASSETBUNDLE 7335 (TRIỆT TIÊU HOÀN TOÀN CÁC WIDGET & OVERLAY)
# ==============================================================================
print("\n[BƯỚC 3/4] Xử lý AssetBundle 7335...")
with open(BACKUP_7335, "rb") as f:
    bundle_header = f.read(6)
    bundle_data = f.read()

env = UnityPy.load(bundle_data)

# 1. Cập nhật TextAsset banner và TextAsset version
for obj in env.objects:
    if obj.type.name == "TextAsset":
        d = obj.read()
        name = getattr(d, 'm_Name', getattr(d, 'name', ''))
        if name == "banner":
            tree = obj.read_typetree()
            tree["m_Script"] = bytes(new_banner).decode("utf8", "surrogateescape")
            obj.save_typetree(tree)
            print("  ✓ [TEXTASSET] Đã nạp custom_vietnamese_banner.bytes")
        elif name == "version":
            tree = obj.read_typetree()
            tree["m_Script"] = "0.1.18.2"
            obj.save_typetree(tree)
            print("  ✓ [VERSION] TextAsset version = '0.1.18.2'")

# 2. VÔ HIỆU HÓA TẤT CẢ CANVAS (church_overlay_ui, banner_ui, minimized_ui)
for obj in env.objects:
    if obj.type.name == "Canvas":
        tree = obj.read_typetree()
        tree["m_Enabled"] = 0
        obj.save_typetree(tree)
        print(f"  ✓ Đã vô hiệu hóa Canvas {obj.path_id} (m_Enabled = 0)")

# 3. TRIỆT TIÊU TẤT CẢ WIDGET GAMEOBJECTS (m_IsActive = False)
ALL_WIDGET_GOS = [
    -6726971078840763528, -1025677653856999440, 6056890383052536343, 7704707874778712529,
    7536197631690499365, -20086240089568681, -2071702513459217074, 181400006959864772,
    7250074427761885983, -1463856590350020512, 7467522749047537223, 7467741852203692155,
    -4066028875868062466, 7499150814022069514, -4540523652528250030, -4906750273294199586,
    2918208893904519460, -8024884520056744080, -3260962330874098076, -1517345643564174617,
    8843252610859507222, 553793050912863030, 461669180457194135, 7033701443070015069,
    -9026316527257111624, -1425498842308796905, 867746991295884481, -8380188771950577069,
    -4951845860935318960
]

for obj in env.objects:
    if obj.type.name == "GameObject":
        d = obj.read()
        if obj.path_id in ALL_WIDGET_GOS or d.m_Name in [
            "church_overlay_ui", "banner_ui", "minimized_ui", "Church", "BannerImage",
            "BtnDisable", "buttons", "CloseButton", "MinimizeButton", "ExpandButton",
            "TimeNight", "TimeEvening", "TimeDay", "TimeMorning", "Export", "VersionText", "flavor_text"
        ]:
            d.m_IsActive = False
            d.save()

# 4. THU NHỎ & ĐẨY RECTTRANSFORMS RA KHỎI KHÔNG GIAN HIỂN THỊ (-9999, -9999)
for obj in env.objects:
    if obj.type.name == "RectTransform":
        tree = obj.read_typetree()
        go_pid = tree.get("m_GameObject", {}).get("m_PathID")
        if go_pid in ALL_WIDGET_GOS:
            tree["m_SizeDelta"] = {"x": 0.0, "y": 0.0}
            tree["m_AnchoredPosition"] = {"x": -9999.0, "y": -9999.0}
            obj.save_typetree(tree)

# 5. XÓA SẠCH TEXT & LÀM TRONG SUỐT MONOBEHAVIOUR TRONG CÁC WIDGET
for obj in env.objects:
    if obj.type.name == "MonoBehaviour":
        try:
            tree = obj.read_typetree()
            go_pid = tree.get("m_GameObject", {}).get("m_PathID")
            if go_pid in ALL_WIDGET_GOS:
                if "m_Text" in tree: tree["m_Text"] = ""
                if "m_text" in tree: tree["m_text"] = ""
                if "m_fontSize" in tree: tree["m_fontSize"] = 0.0
                if "m_Color" in tree: tree["m_Color"] = {"r": 0.0, "g": 0.0, "b": 0.0, "a": 0.0}
                obj.save_typetree(tree)
        except Exception:
            pass

# 6. NẠP TOÀN BỘ 189 TEXTURE TIẾNG VIỆT
VN_TEX_DIR = os.path.join(WORKSPACE, "vietnamese_textures")
CUSTOM_IMG_DIR = os.path.join(WORKSPACE, "custom_images")
tex_count = 0
for obj in env.objects:
    if obj.type.name == "Texture2D":
        d = obj.read()
        name = getattr(d, 'm_Name', getattr(d, 'name', ''))
        custom_candidate = os.path.join(CUSTOM_IMG_DIR, f"{name}.png")
        vn_candidate = os.path.join(VN_TEX_DIR, f"{name}.png")
        chosen = None
        if os.path.exists(custom_candidate): chosen = custom_candidate
        elif os.path.exists(vn_candidate): chosen = vn_candidate
        
        if chosen:
            img = Image.open(chosen)
            d.image = img
            d.save()
            tex_count += 1

print(f"  ✓ Đã nạp {tex_count} texture Tiếng Việt vào bundle")

# ==============================================================================
# BƯỚC 4: GHI FILE VÀO PERSISTENTROOT
# ==============================================================================
print("\n[BƯỚC 4/4] Đóng gói UnityFS và ghi vào PersistentRoot...")
modified_bytes = env.file.save()
with open(TARGET_7335, "wb") as f:
    f.write(bundle_header)
    f.write(modified_bytes)

print(f"  ✓ Đã ghi đè bundle 7335: {TARGET_7335} ({os.path.getsize(TARGET_7335):,} bytes)")
print("\n🎉 TRIỂN KHAI HOÀN TẤT: KHÔNG CÒN NÚT ON/OFF, KHÔNG CÒN FCK OFF, BẢNG CẢNH BÁO ĐÃ SẴN SÀNG!")
print("="*80)
