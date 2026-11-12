const fs = require('fs');
const path = require('path');
const resedit = require('resedit');

try {
  const electronExe = path.join(__dirname, 'node_modules/electron/dist/electron.exe');
  const iconIco = path.join(__dirname, 'public/reverse1999-icon.ico');

  if (!fs.existsSync(electronExe)) {
    console.log('electron.exe not found at:', electronExe);
    process.exit(1);
  }

  const exeData = fs.readFileSync(electronExe);
  const exe = resedit.NtExecutable.from(exeData);
  const res = resedit.NtExecutableResource.from(exe);

  const iconData = fs.readFileSync(iconIco);
  const iconFile = resedit.Data.IconFile.from(iconData);

  resedit.Resource.IconGroupEntry.replaceIconsForResource(
    res.entries,
    1, // MAINICON id
    1033, // English US or language neutral
    iconFile.icons.map((item) => item.data)
  );

  res.outputResource(exe);
  const newExe = exe.generate();
  fs.writeFileSync(electronExe, Buffer.from(newExe));

  console.log('Successfully patched node_modules/electron/dist/electron.exe with new app icon!');
} catch (err) {
  console.error('Error patching electron.exe:', err);
}
