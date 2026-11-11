import os
import sys
import json
import time
import urllib.request
import urllib.parse
from concurrent.futures import ThreadPoolExecutor, as_completed

WORKSPACE = os.path.dirname(os.path.abspath(__file__))
FREQ_PATH = os.path.join(WORKSPACE, "russian_word_frequencies.json")
DICT_PATH = os.path.join(WORKSPACE, "ru_vi_translation_dict.json")

# Terminology override dành riêng cho Reverse: 1999
GAME_TERMS = {
    # Nhân vật chính & phe phái
    "вертин": "Vertin", "сонетто": "Sonetto", "регулус": "Regulus", "лиля": "Lilya",
    "сотби": "Sotheby", "друвис": "Druvis", "какания": "Kakania", "семмельвайс": "Semmelweis",
    "виндсонг": "Windsong", "люси": "Lucy", "матильда": "Matilda", "маркус": "Marcus",
    "павлова": "Pavlov", "павлов": "Pavlov", "вендиката": "Vindicta",
    
    # Thuật ngữ game Reverse: 1999
    "унилог": "Unilog", "унилога": "Unilog", "унилоги": "Unilog", "унилогов": "Unilog",
    "арканист": "pháp sư huyền bí", "арканиста": "pháp sư huyền bí", "арканисты": "pháp sư huyền bí",
    "шторм": "Cơn Bão", "шторма": "Cơn Bão", "шторме": "Cơn Bão", "шторму": "Cơn Bão",
    "прозрение": "Đột phá", "прозрения": "Đột phá", "прозрении": "Đột phá",
    "психокуб": "Psychocube", "психокуба": "Psychocube", "психокубы": "Psychocube",
    "лимбо": "Limbo", "лимб": "Limbo",
    "глушь": "Vùng Hoang Dã", "глуши": "Vùng Hoang Dã",
    "ультимейт": "nghi thức tối thượng", "заклинание": "thuật thức",
}

print("=" * 70)
print("🌐 BỘ DỊCH TOÀN BỘ TỪ TIẾNG NGA CÒN LẠI TRONG REVERSE: 1999")
print("=" * 70)

# 1. Nạp từ điển đã có
master_dict = {}
if os.path.isfile(DICT_PATH):
    try:
        with open(DICT_PATH, "r", encoding="utf-8") as f:
            master_dict = json.load(f)
        print(f"📖 Đã nạp từ điển hiện có: {len(master_dict):,} từ")
    except Exception as e:
        print(f"⚠️ Không đọc được từ điển cũ: {e}")

# Áp dụng game terms
master_dict.update(GAME_TERMS)

# 2. Nạp danh sách từ cần dịch
if not os.path.isfile(FREQ_PATH):
    print("❌ Không tìm thấy russian_word_frequencies.json. Hãy chạy extract_ru_words.py trước.")
    sys.exit(1)

with open(FREQ_PATH, "r", encoding="utf-8") as f:
    word_freq_list = json.load(f)

# Lọc các từ chưa có trong từ điển
missing_words = [w for w, count in word_freq_list if w not in master_dict]
print(f"📊 Tổng số từ cần dịch thêm: {len(missing_words):,} từ")

def translate_chunk(chunk):
    """Dịch một mảng các từ tiếng Nga trong một request duy nhất."""
    if not chunk:
        return {}
    payload = "\n".join(chunk)
    
    # Danh sách các endpoints và clients để xoay tua nếu gặp lỗi hoặc rate limit
    endpoints = [
        "https://translate.googleapis.com/translate_a/single?client=dict-chrome-ex&sl=ru&tl=vi&dt=t&q=",
        "https://clients5.google.com/translate_a/t?client=dict-chrome-ex&sl=ru&tl=vi&q=",
        "https://translate.googleapis.com/translate_a/single?client=gtx&sl=ru&tl=vi&dt=t&q=",
    ]
    
    for ep in endpoints:
        url = ep + urllib.parse.quote(payload)
        req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"})
        for attempt in range(2):
            try:
                with urllib.request.urlopen(req, timeout=10) as resp:
                    raw_data = resp.read().decode("utf-8")
                    res = json.loads(raw_data)
                    if isinstance(res, list) and len(res) > 0:
                        if isinstance(res[0], list):
                            trans_text = "".join([part[0] for part in res[0] if part and part[0]])
                        elif isinstance(res[0], str):
                            trans_text = "\n".join(res)
                        else:
                            trans_text = str(res)
                        lines = [l.strip() for l in trans_text.split("\n")]
                        if len(lines) == len(chunk):
                            return dict(zip(chunk, lines))
            except Exception:
                time.sleep(0.5)
                
    # Fallback từng từ nếu batch bị lệch số dòng
    single_res = {}
    for w in chunk:
        for ep in endpoints:
            try:
                url = ep + urllib.parse.quote(w)
                req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
                with urllib.request.urlopen(req, timeout=5) as resp:
                    res = json.loads(resp.read().decode("utf-8"))
                    if isinstance(res, list) and len(res) > 0:
                        if isinstance(res[0], list) and len(res[0]) > 0 and len(res[0][0]) > 0:
                            single_res[w] = res[0][0][0].strip()
                        elif isinstance(res[0], str):
                            single_res[w] = res[0].strip()
                        break
            except Exception:
                pass
    return single_res

def run_translation_tier(words_to_translate, max_workers=6, chunk_size=50):
    chunks = [words_to_translate[i:i + chunk_size] for i in range(0, len(words_to_translate), chunk_size)]
    total_chunks = len(chunks)
    completed_words = 0
    start_time = time.time()
    
    print(f"\n🚀 Đang dịch {len(words_to_translate):,} từ ({total_chunks} batches, {max_workers} threads)...")
    
    with ThreadPoolExecutor(max_workers=max_workers) as executor:
        futures = {executor.submit(translate_chunk, chunk): chunk for chunk in chunks}
        
        for idx, future in enumerate(as_completed(futures), 1):
            chunk_res = future.result()
            master_dict.update(chunk_res)
            completed_words += len(chunk_res)
            
            if idx % 10 == 0 or idx == total_chunks:
                elapsed = time.time() - start_time
                wps = completed_words / elapsed if elapsed > 0 else 0
                pct = (completed_words / len(words_to_translate)) * 100
                print(f"   [{idx}/{total_chunks}] Đã dịch {completed_words:,}/{len(words_to_translate):,} từ ({pct:.1f}%) - Tốc độ: {wps:.1f} từ/s")
                
                # Lưu checkpoint mỗi 20 batches
                if idx % 20 == 0 or idx == total_chunks:
                    with open(DICT_PATH, "w", encoding="utf-8") as f:
                        json.dump(master_dict, f, ensure_ascii=False, indent=2)

if __name__ == "__main__":
    limit = None
    if len(sys.argv) > 1:
        try:
            limit = int(sys.argv[1])
        except ValueError:
            pass

    target_words = missing_words[:limit] if limit else missing_words
    if target_words:
        run_translation_tier(target_words)
        print(f"\n🎉 ĐÃ LƯU TOÀN BỘ TỪ ĐIỂN VÀO: {DICT_PATH} ({len(master_dict):,} từ)")
    else:
        print("\n✓ Toàn bộ từ tiếng Nga đã được dịch sẵn trong từ điển!")
