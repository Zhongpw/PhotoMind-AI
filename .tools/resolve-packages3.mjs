// Download ONNX Runtime packages (CPU + GPU Windows) with transitive deps to offline feed.
// Usage: node resolve-packages3.mjs
import { createWriteStream, mkdirSync, existsSync, statSync, renameSync } from 'node:fs';
import { pipeline } from 'node:stream/promises';
import { Readable } from 'node:stream';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const outDir = join(__dirname, '..', '.nuget', 'offline');
mkdirSync(outDir, { recursive: true });

const FLAT = 'https://api.nuget.org/v3-flatcontainer';
const roots = [
  ['microsoft.ml.onnxruntime', '1.32.0'],
  ['microsoft.ml.onnxruntime.gpu.windows', '1.32.0'],
];

async function get(url) {
  const r = await fetch(url, { redirect: 'follow' });
  if (!r.ok) throw new Error('HTTP ' + r.status + ' ' + url);
  return r;
}
async function getJson(url) { return (await get(url)).json(); }
async function getText(url) { return (await get(url)).text(); }

async function download(id, ver) {
  const file = `${id}.${ver}.nupkg`;
  const dest = join(outDir, file);
  if (existsSync(dest) && statSync(dest).size > 0) { console.log('SKIP', id, ver); return; }
  const url = `${FLAT}/${id}/${ver}/${file}`;
  console.log('DOWNLOAD', id, ver);
  const resp = await get(url);
  const tmp = dest + '.part';
  await pipeline(Readable.fromWeb(resp.body), createWriteStream(tmp));
  renameSync(tmp, dest);
  console.log('  OK', (statSync(dest).size / 1024 / 1024).toFixed(1) + 'MB');
}

const seen = new Set();
let downloaded = 0, failed = 0;
const queue = [...roots];
while (queue.length) {
  const [id, ver] = queue.shift();
  const key = id + '/' + ver;
  if (seen.has(key)) continue;
  seen.add(key);
  try {
    await download(id, ver);
    downloaded++;
    // read deps
    const nuspecUrl = `${FLAT}/${id}/${ver}/${id}.nuspec`;
    const xml = await getText(nuspecUrl);
    const depRe = /<dependency\s+id="([^"]+)"\s+version="([^"]+)"/g;
    let m;
    while ((m = depRe.exec(xml)) !== null) {
      const depId = m[1].toLowerCase();
      let depVer = m[2];
      if (depVer.includes(',')) depVer = depVer.split(',')[0]; // version ranges like [1.0.0, )
      depVer = depVer.replace(/[\[\]\(\)\s]/g, '');
      if (!seen.has(depId + '/' + depVer)) queue.push([depId, depVer]);
    }
  } catch (e) {
    console.log('  ERR', id, ver, e.message);
    failed++;
  }
}
console.log(`DONE downloaded=${downloaded} failed=${failed}`);
process.exit(failed === 0 ? 0 : 1);
