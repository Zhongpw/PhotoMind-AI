import { writeFileSync, mkdirSync, existsSync } from 'node:fs';
import { join } from 'node:path';

const outDir = process.argv[2] || '.nuget/offline';
const packages = [
  ['microsoft.netcore.app.runtime.win-x64', '8.0.34'],
  ['microsoft.windowsdesktop.app.runtime.win-x64', '8.0.34'],
  ['microsoft.aspnetcore.app.runtime.win-x64', '8.0.34'],
];

mkdirSync(outDir, { recursive: true });

let failed = 0;
for (const [id, ver] of packages) {
  const file = `${id}.${ver}.nupkg`;
  const dest = join(outDir, file);
  if (existsSync(dest)) {
    console.log(`SKIP (exists) ${file}`);
    continue;
  }
  const url = `https://api.nuget.org/v3-flatcontainer/${id}/${ver}/${file}`;
  try {
    const res = await fetch(url, { redirect: 'follow' });
    if (!res.ok) {
      console.log(`FAIL ${res.status} ${file}`);
      failed++;
      continue;
    }
    const buf = Buffer.from(await res.arrayBuffer());
    writeFileSync(dest, buf);
    console.log(`OK ${file} (${(buf.length / 1024 / 1024).toFixed(1)} MB)`);
  } catch (e) {
    console.log(`ERROR ${file}: ${e.message}`);
    failed++;
  }
}
console.log(`DONE failed=${failed}`);
