import os
import sys
import json
import re
import hashlib
import struct
from Crypto.Cipher import AES
import UnityPy

WORKSPACE = os.path.dirname(os.path.abspath(__file__))
BANNER_PATH = os.path.join(WORKSPACE, "custom_vietnamese_banner.bytes")
BUNDLE_PATH = os.path.join(WORKSPACE, "7335e2dfbf8ec0e4389bc697130b63e2.dat")
DICT_PATH = os.path.join(WORKSPACE, "ru_vi_translation_dict.json")

print("=" * 70)
print("📥 ÁP DỤNG BẢN DỊCH TIẾNG VIỆT VÀO CARRIER BANNER.BYTES & ASSETBUNDLE")
print("=" * 70)

# 1. Nạp từ điển
if not os.path.isfile(DICT_PATH):
    print("❌ Không tìm thấy từ điển:", DICT_PATH)
    sys.exit(1)

with open(DICT_PATH, "r", encoding="utf-8") as f:
    tr_dict = json.load(f)
print(f"📖 Đã nạp từ điển: {len(tr_dict):,} từ tiếng Việt")

# 2. Giải mã carrier
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

cyrillic = re.compile(r'[\u0400-\u04FF]+')

def repl_word(m):
    w = m.group(0)
    lw = w.lower()
    tr = tr_dict.get(lw)
    if not tr:
        return w
    if w.isupper() and len(w) > 1:
        return tr.upper()
    if w[0].isupper():
        return tr[0].upper() + tr[1:] if len(tr) > 1 else tr.upper()
    return tr

def translate_str(s):
    if not isinstance(s, str) or not s:
        return s
    return cyrillic.sub(repl_word, s)

print("\n1. Đang áp dụng vào mục [l] (UI & Hệ thống)...")
for k, v in data['l'].items():
    if isinstance(v, str) and cyrillic.search(v):
        data['l'][k] = translate_str(v)

print("2. Đang áp dụng vào mục [p] (Thơ dẫn truyện)...")
for k, v in data['p'].items():
    if isinstance(v, str) and cyrillic.search(v):
        data['p'][k] = translate_str(v)

print("3. Đang áp dụng vào mục [s] (Cốt truyện)...")
for ch_id, lines in data['s'].items():
    if isinstance(lines, dict):
        for line_id, item in lines.items():
            if isinstance(item, list) and len(item) > 0 and isinstance(item[0], str) and cyrillic.search(item[0]):
                item[0] = translate_str(item[0])

print("4. Đang áp dụng vào mục [c] (Thoại nhân vật)...")
for char_id, acts in data['c'].items():
    if isinstance(acts, dict):
        for act_id, text in acts.items():
            if isinstance(text, str) and cyrillic.search(text):
                acts[act_id] = translate_str(text)

# 3. Đếm số từ tiếng Nga còn sót lại
ru_remaining = 0
for v in data['l'].values():
    if isinstance(v, str) and cyrillic.search(v): ru_remaining += 1
for v in data['p'].values():
    if isinstance(v, str) and cyrillic.search(v): ru_remaining += 1
for lines in data['s'].values():
    if isinstance(lines, dict):
        for item in lines.values():
            if isinstance(item, list) and len(item) > 0 and isinstance(item[0], str) and cyrillic.search(item[0]):
                ru_remaining += 1
for acts in data['c'].values():
    if isinstance(acts, dict):
        for text in acts.values():
            if isinstance(text, str) and cyrillic.search(text):
                ru_remaining += 1

print(f"\n📊 Số câu còn dính tiếng Nga: {ru_remaining:,} (Trước đó: 184,912)")

# 4. Mã hóa AES và lưu banner.bytes
print("\n🔒 Đang mã hóa AES-256-CBC và lưu custom_vietnamese_banner.bytes...")
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
print(f"✓ Đã lưu banner.bytes: {len(new_banner_bytes):,} bytes")

# 5. Cập nhật TextAsset banner trong bundle 7335
if os.path.isfile(BUNDLE_PATH):
    print("\n📦 Đang nạp vào AssetBundle 7335...")
    with open(BUNDLE_PATH, "rb") as f:
        header = f.read(6)
        bdata = f.read()
    env = UnityPy.load(bdata)
    for obj in env.objects:
        if obj.type.name == "TextAsset":
            d = obj.read()
            name = getattr(d, 'm_Name', getattr(d, 'name', ''))
            if name == "banner":
                tree = obj.read_typetree()
                tree["m_Script"] = bytes(new_banner_bytes).decode("utf8", "surrogateescape")
                obj.save_typetree(tree)
                break
    packed = env.file.save(packer="lz4")
    with open(BUNDLE_PATH, "wb") as f:
        f.write(header)
        f.write(packed)
    print(f"✓ Đã cập nhật xong AssetBundle: {BUNDLE_PATH}")
