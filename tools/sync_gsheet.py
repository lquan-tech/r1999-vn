#!/usr/bin/env python3
"""
Sync Vietnamese translations from Google Sheet (re1999-vn) into:
  1. public/fileviethoa/configs/language/json_language_en.json.dat
  2. public/fileviethoa/configs/language/json_language_server_en.json.dat
  3. public/fileviethoa/manifest.json (+ optional public/viethoaversion.txt)
  4. Optional --install into the local Reverse: 1999 game directory (with localhash update).

Works both inside Antigravity (using `gsheets` CLI) and standalone via `--csv-dir`
or Google Sheets CSV export URL (when the sheet is viewable with link).
"""

import argparse
import csv
import hashlib
import json
import os
import shutil
import subprocess
import sys
import tempfile
import urllib.request
from typing import Dict, Tuple

from cryptography.hazmat.primitives import padding
from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes

SPREADSHEET_ID = "1-ioeJWqe4r72BTY0F0rA6dd0pjsM1OQS2Z0re6ktbYo"
TABS = [
    ("UI", 0),
    ("Story", 90166683),
    ("Dialogue", 2080606176),
    ("Item", 1212513200),
    ("Skill", 273568440),
]

AES_KEY = b"@_#*&Reverse2806                "
AES_IV = b"!_#@2022_Skyfly)"
HEADER_LEN = 48

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PUBLIC_VH = os.path.join(REPO_ROOT, "public", "fileviethoa")
DEFAULT_GAME_DIR = r"C:\Program Files (x86)\reverse1999_global\Reverse1999en"
RT_STRINGS_OUT = r"C:\Users\Long Quam\Downloads\RTlauncher\translations\vi\strings\vi_strings.json"


def md5_file(path: str) -> str:
    h = hashlib.md5()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def read_dat(path: str) -> Tuple[bytes, str]:
    with open(path, "rb") as f:
        raw = f.read()
    header = raw[:HEADER_LEN]
    body = raw[HEADER_LEN:]
    decryptor = Cipher(algorithms.AES(AES_KEY), modes.CBC(AES_IV)).decryptor()
    padded = decryptor.update(body) + decryptor.finalize()
    unpadder = padding.PKCS7(128).unpadder()
    plain = unpadder.update(padded) + unpadder.finalize()
    return header, plain.decode("utf-8")


def write_dat(path: str, header: bytes, text: str) -> None:
    data = text.encode("utf-8")
    padder = padding.PKCS7(128).padder()
    padded = padder.update(data) + padder.finalize()
    encryptor = Cipher(algorithms.AES(AES_KEY), modes.CBC(AES_IV)).encryptor()
    body = encryptor.update(padded) + encryptor.finalize()
    os.makedirs(os.path.dirname(path), exist_ok=True)
    tmp = path + ".tmp"
    with open(tmp, "wb") as f:
        f.write(header)
        f.write(body)
    os.replace(tmp, path)


def patch_table(src: str, dst: str, mapping: Dict[str, str]) -> Tuple[int, int]:
    header, text = read_dat(src)
    doc = json.loads(text)
    if not isinstance(doc, list) or len(doc) < 2 or not isinstance(doc[1], list):
        raise ValueError(f"Unexpected language table structure in {src}")

    changed = 0
    total = 0
    for pair in doc[1]:
        if not isinstance(pair, list) or len(pair) < 2:
            continue
        total += 1
        k = pair[0]
        if k in mapping:
            val = mapping[k]
            if val and val != pair[1]:
                pair[1] = val
                changed += 1

    write_dat(dst, header, json.dumps(doc, ensure_ascii=False, separators=(",", ":")))
    return changed, total


def export_tabs_to_dir(out_dir: str) -> Dict[str, str]:
    gsheets_bin = shutil.which("gsheets")
    paths: Dict[str, str] = {}
    for tab_name, gid in TABS:
        csv_path = os.path.join(out_dir, f"{tab_name}.csv")
        if gsheets_bin:
            proc = subprocess.run(
                [gsheets_bin, "readonly", "export", SPREADSHEET_ID, csv_path, "--format", "csv", "--sheet", tab_name],
                capture_output=True,
                text=True,
                encoding="utf-8",
            )
            if proc.returncode != 0:
                raise RuntimeError(f"gsheets export {tab_name} failed: {proc.stderr or proc.stdout}")
        else:
            url = f"https://docs.google.com/spreadsheets/d/{SPREADSHEET_ID}/export?format=csv&gid={gid}"
            try:
                urllib.request.urlretrieve(url, csv_path)
            except Exception as e:
                raise RuntimeError(
                    f"Không tải được tab {tab_name} (cần bật quyền xem link trên Google Sheet hoặc truyền --csv-dir): {e}"
                ) from e
        paths[tab_name] = csv_path
    return paths


def parse_translations(csv_paths: Dict[str, str]) -> Dict[str, str]:
    merged: Dict[str, str] = {}
    for tab_name, csv_path in csv_paths.items():
        count = 0
        with open(csv_path, "r", encoding="utf-8-sig", newline="") as f:
            reader = csv.DictReader(f)
            for row in reader:
                key = (row.get("key") or "").strip()
                vi = row.get("vi") or ""
                if key and vi.strip():
                    merged[key] = vi
                    count += 1
        print(f"  [{tab_name:8}] {count:,} translated rows")
    return merged


def main():
    parser = argparse.ArgumentParser(description="Sync translations from Google Sheet to r1999-vn")
    parser.add_argument("--csv-dir", help="Optional folder containing UI.csv, Story.csv, Dialogue.csv, Item.csv, Skill.csv")
    parser.add_argument("--game-dir", default=DEFAULT_GAME_DIR, help="Reverse: 1999 game root for stock language tables / install")
    parser.add_argument("--version", help="Optional new viethoa version (e.g. 1.1.1) to write into manifest & viethoaversion.txt")
    parser.add_argument("--install", action="store_true", help="Also copy updated language tables + localhash into --game-dir")
    parser.add_argument("--dry-run", action="store_true", help="Only fetch and count translated rows without modifying files")
    args = parser.parse_args()

    tmp_dir = None
    if args.csv_dir:
        csv_paths = {name: os.path.join(args.csv_dir, f"{name}.csv") for name, _ in TABS}
    else:
        tmp_dir = tempfile.mkdtemp(prefix="r1999_sync_")
        print("Exporting sheets from Google Sheets...")
        csv_paths = export_tabs_to_dir(tmp_dir)

    print("Parsing translated rows:")
    translations = parse_translations(csv_paths)
    print(f"Total translated keys: {len(translations):,}")

    if args.dry_run:
        return

    if os.path.isdir(os.path.dirname(RT_STRINGS_OUT)):
        with open(RT_STRINGS_OUT, "w", encoding="utf-8") as f:
            json.dump(translations, f, ensure_ascii=False, indent=2)
        print(f"Updated {RT_STRINGS_OUT}")

    win_lang_dir = os.path.join(args.game_dir, "reverse1999_Data", "StreamingAssets", "Windows", "configs", "language")
    per_lang_dir = os.path.join(args.game_dir, "reverse1999_Data", "StreamingAssets", "PersistentRoot", "configs", "language")
    out_lang_dir = os.path.join(PUBLIC_VH, "configs", "language")

    for fn in ("json_language_en.json.dat", "json_language_server_en.json.dat"):
        src_win = os.path.join(win_lang_dir, fn)
        src_per = os.path.join(per_lang_dir, fn)
        src = src_win if os.path.exists(src_win) else src_per
        if not os.path.exists(src):
            raise FileNotFoundError(f"Missing base language table: {src}")
        dst = os.path.join(out_lang_dir, fn)
        changed, total = patch_table(src, dst, translations)
        print(f"Patched {fn}: {changed:,}/{total:,} rows")

    manifest_path = os.path.join(PUBLIC_VH, "manifest.json")
    with open(manifest_path, "r", encoding="utf-8") as f:
        manifest = json.load(f)

    if args.version:
        manifest["version"] = args.version
        with open(os.path.join(REPO_ROOT, "public", "viethoaversion.txt"), "w", encoding="utf-8", newline="\n") as f:
            f.write(args.version.strip() + "\n")

    for entry in manifest.get("files", []):
        fpath = os.path.join(PUBLIC_VH, entry["targetRelPath"].replace("/", os.sep))
        if os.path.exists(fpath):
            entry["md5"] = md5_file(fpath)
            entry["size"] = os.path.getsize(fpath)

    with open(manifest_path, "w", encoding="utf-8") as f:
        json.dump(manifest, f, ensure_ascii=False, indent=2)
    print(f"Updated {manifest_path} (version={manifest.get('version')})")

    if args.install:
        per_root = os.path.join(args.game_dir, "reverse1999_Data", "StreamingAssets", "PersistentRoot")
        lh_path = os.path.join(per_root, "localhash")
        lh = json.load(open(lh_path, "r", encoding="utf-8"))
        for entry in manifest["files"]:
            lh[entry["localhashKey"]] = entry["md5"]

        stage = tempfile.mkdtemp(prefix="r1999_inst_")
        lh_stage = os.path.join(stage, "localhash")
        json.dump(lh, open(lh_stage, "w", encoding="utf-8"), ensure_ascii=False, separators=(",", ":"))

        try:
            for entry in manifest["files"]:
                rel = entry["targetRelPath"].replace("/", os.sep)
                src = os.path.join(PUBLIC_VH, rel)
                dst = os.path.join(per_root, rel)
                os.makedirs(os.path.dirname(dst), exist_ok=True)
                shutil.copy2(src, dst)
            shutil.copy2(lh_stage, lh_path)
            print("Installed updated patch into game directory.")
        except PermissionError:
            ps1 = os.path.join(stage, "apply.ps1")
            lines = []
            for entry in manifest["files"]:
                rel = entry["targetRelPath"].replace("/", os.sep)
                src = os.path.join(PUBLIC_VH, rel).replace("'", "''")
                dst = os.path.join(per_root, rel).replace("'", "''")
                lines.append(f"Copy-Item -Force '{src}' '{dst}'")
            lines.append(f"Copy-Item -Force '{lh_stage.replace(chr(39), chr(39)*2)}' '{lh_path.replace(chr(39), chr(39)*2)}'")
            open(ps1, "w", encoding="utf-8").write("\r\n".join(lines))
            subprocess.run(
                ["powershell", "-NoProfile", "-Command", f"Start-Process powershell.exe -ArgumentList '-NoProfile -ExecutionPolicy Bypass -File \"{ps1}\"' -Verb RunAs -Wait -WindowStyle Hidden"],
                check=True,
            )
            print("Installed updated patch into game directory (UAC elevated).")


if __name__ == "__main__":
    main()
