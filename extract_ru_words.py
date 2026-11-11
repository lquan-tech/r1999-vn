import json
import hashlib
from Crypto.Cipher import AES
import re
from collections import Counter
import os

WORKSPACE = os.path.dirname(os.path.abspath(__file__))
BANNER_PATH = os.path.join(WORKSPACE, "custom_vietnamese_banner.bytes")
OUTPUT_WORDS_PATH = os.path.join(WORKSPACE, "russian_word_frequencies.json")

print("=" * 70)
print("🔍 TRÍCH XUẤT TẦN SUẤT TỪ TIẾNG NGA CÒN SÓT TRONG GAME")
print("=" * 70)

with open(BANNER_PATH, "rb") as f:
    raw = f.read()

jpeg = raw[:582344]
key = hashlib.sha256(jpeg).digest()
iv = raw[582346:582346+16]
b1_len = int.from_bytes(raw[582362:582366], 'little')
b2_offset = 582366 + b1_len
b2_len = int.from_bytes(raw[b2_offset:b2_offset+4], 'little')
ct = raw[b2_offset+4 : b2_offset+4+b2_len]

cipher = AES.new(key, AES.MODE_CBC, iv)
pt = cipher.decrypt(ct)
pad = pt[-1]
pt = pt[:-pad]
data = json.loads(pt.decode('utf-8'))

cyrillic = re.compile(r'[\u0400-\u04FF]+')
word_counts = Counter()

def scan(text):
    if isinstance(text, str):
        for w in cyrillic.findall(text):
            word_counts[w.lower()] += 1

print("1. Đang quét mục [l] (UI & Hệ thống)...")
for v in data['l'].values(): scan(v)

print("2. Đang quét mục [p] (Thơ dẫn truyện)...")
for v in data['p'].values(): scan(v)

print("3. Đang quét mục [s] (Cốt truyện)...")
for lines in data['s'].values():
    if isinstance(lines, dict):
        for item in lines.values():
            if isinstance(item, list) and len(item) > 0: scan(item[0])

print("4. Đang quét mục [c] (Thoại nhân vật)...")
for acts in data['c'].values():
    if isinstance(acts, dict):
        for text in acts.values(): scan(text)

total_occ = sum(word_counts.values())
print(f"\n📊 Tổng số lần xuất hiện từ tiếng Nga: {total_occ:,}")
print(f"📊 Tổng số từ tiếng Nga độc nhất:      {len(word_counts):,}")

sorted_words = word_counts.most_common()
with open(OUTPUT_WORDS_PATH, "w", encoding="utf-8") as f:
    json.dump(sorted_words, f, ensure_ascii=False, indent=2)

print(f"✓ Đã lưu danh sách vào: {OUTPUT_WORDS_PATH}")
