// Downloads the .NET 8 SDK (win-x64) zip using Node's modern TLS (fetch).
// Usage: node .tools/download-sdk.mjs <url> <dest>
import { createWriteStream, mkdirSync } from 'node:fs';
import { dirname } from 'node:path';

const url = process.argv[2];
const dest = process.argv[3];
if (!url || !dest) {
  console.error('usage: node download-sdk.mjs <url> <dest>');
  process.exit(2);
}

mkdirSync(dirname(dest), { recursive: true });
const res = await fetch(url, { redirect: 'follow' });
if (!res.ok || !res.body) {
  console.error(`HTTP ${res.status} ${res.statusText}`);
  process.exit(1);
}

const total = Number(res.headers.get('content-length') || 0);
let received = 0;
const ws = createWriteStream(dest);
const reader = res.body.getReader();
const started = Date.now();
let lastPct = -1;
while (true) {
  const { done, value } = await reader.read();
  if (done) break;
  received += value.length;
  ws.write(Buffer.from(value));
  if (total > 0) {
    const pct = Math.floor((received / total) * 100);
    if (pct >= lastPct + 10) {
      lastPct = pct;
      const mb = (received / 1048576).toFixed(1);
      const totalMb = (total / 1048576).toFixed(1);
      console.log(`${pct}%  ${mb}/${totalMb} MB  (${((Date.now()-started)/1000).toFixed(0)}s)`);
    }
  }
}
await new Promise((res, rej) => ws.end((e) => (e ? rej(e) : res())));
console.log(`DONE ${dest}  ${(received/1048576).toFixed(1)} MB`);
