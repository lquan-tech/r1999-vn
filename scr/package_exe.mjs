import fs from 'node:fs';
import path from 'node:path';
import { execSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const workspaceRoot = __dirname;
const launcherDir = path.join(workspaceRoot, 'R199Launc');
const releaseDir = path.join(launcherDir, 'release');

const target1 = path.join(workspaceRoot, 'viLauncher.exe');
const target2 = path.join(workspaceRoot, 'viLauncher2.exe');

console.log('='.repeat(70));
console.log('🚀 ĐÓNG GÓI TỰ ĐỘNG LAUNCHER SANG EXECUTABLE (viLauncher.exe / viLauncher2.exe)');
console.log('='.repeat(70));

// 1. Biên dịch frontend & backend
console.log('\n[1/3] Đang biên dịch mã nguồn Launcher (Vite + TypeScript + ESBuild)...');
execSync('npm run build', { cwd: launcherDir, stdio: 'inherit' });

// 2. Đóng gói Electron bằng electron-builder
console.log('\n[2/3] Đang đóng gói tệp cài đặt (electron-builder)...');
execSync('npx electron-builder --win nsis', { cwd: launcherDir, stdio: 'inherit' });

// 3. Tìm tệp installer exe vừa tạo trong release/
const releaseFiles = fs.readdirSync(releaseDir);
const exeCandidates = releaseFiles
  .filter((f) => f.endsWith('.exe') && !f.includes('unpacked') && !f.includes('blockmap'))
  .map((f) => ({ name: f, time: fs.statSync(path.join(releaseDir, f)).mtimeMs }))
  .sort((a, b) => b.time - a.time);
const exeFile = exeCandidates[0]?.name;

if (!exeFile) {
  console.error('\n❌ Không tìm thấy tệp .exe trong thư mục release:', releaseDir);
  process.exit(1);
}

const builtExePath = path.join(releaseDir, exeFile);
const builtSizeMB = (fs.statSync(builtExePath).size / (1024 * 1024)).toFixed(1);

console.log(`\n[3/3] Tệp đóng gói mới nhất: ${exeFile} (${builtSizeMB} MB)`);

// 4. Áp dụng quy tắc quản lý phiên bản viLauncher.exe / viLauncher2.exe
if (!fs.existsSync(target1)) {
  fs.copyFileSync(builtExePath, target1);
  console.log(`\n🎉 ĐÃ TẠO BẢN GỐC THÀNH CÔNG:`);
  console.log(`   👉 ${target1} (${builtSizeMB} MB)`);
} else {
  // viLauncher.exe đã tồn tại -> giữ làm backup, tạo hoặc ghi đè viLauncher2.exe
  const backupSizeMB = (fs.statSync(target1).size / (1024 * 1024)).toFixed(1);
  fs.copyFileSync(builtExePath, target2);
  console.log(`\n🎉 ĐÃ CẬP NHẬT BẢN VÁ LỖI THÀNH CÔNG:`);
  console.log(`   🛡️ Bản sao lưu gốc (giữ nguyên): viLauncher.exe (${backupSizeMB} MB)`);
  console.log(`   👉 Bản cập nhật/vá lỗi mới nhất:  viLauncher2.exe (${builtSizeMB} MB)`);
}

console.log('='.repeat(70));
