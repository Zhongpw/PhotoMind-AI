// Robust downloader: retry + resume (Range) + streaming SHA-512 verification.
// Usage: node download.mjs <url> <outPath> <expectedSha512Hex>
import { createWriteStream, createReadStream } from 'node:fs';
import { stat, unlink } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { pipeline } from 'node:stream/promises';

const [url, outPath, expectedSha] = process.argv.slice(2);
const MAX_ATTEMPTS = 8;
const BASE_DELAY = 3000;
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

function fmt(mb) { return (mb / 1048576).toFixed(1) + 'MB'; }

async function sha512File(path) {
  const h = createHash('sha512');
  await pipeline(createReadStream(path), h);
  return h.digest('hex');
}

let attempt = 0;
let lastErr = null;
while (attempt < MAX_ATTEMPTS) {
  attempt++;
  try {
    let start = 0;
    try { start = (await stat(outPath)).size; } catch { start = 0; }
    const headers = {};
    if (start > 0) headers['Range'] = `bytes=${start}-`;
    console.log(`[attempt ${attempt}] GET ${url}${start > 0 ? ' (resume @' + fmt(start) + ')' : ''}`);
    const res = await fetch(url, { headers, redirect: 'follow' });
    if (!res.ok && res.status !== 206) throw new Error('HTTP ' + res.status);
    const resume = res.status === 206;
    const contentLen = Number(res.headers.get('content-length') || 0);
    const total = (resume ? start : 0) + contentLen;
    const flags = resume ? 'a' : 'w';
    const ws = createWriteStream(outPath, { flags });
    let received = resume ? start : 0;
    const reader = res.body.getReader();
    let lastPct = -1;
    while (true) {
      const { done, value } = await reader.read();
      if (done) break;
      received += value.length;
      if (!ws.write(Buffer.from(value))) await new Promise((r) => ws.once('drain', r));
      const pct = total > 0 ? Math.floor((received / total) * 100) : 0;
      if (pct >= lastPct + 10) { lastPct = pct; console.log(`  ${pct}%  ${fmt(received)} / ${fmt(total)}`); }
    }
    await new Promise((resolve, reject) => ws.end((e) => (e ? reject(e) : resolve())));
    console.log(`  downloaded ${fmt(received)}`);

    // Verify SHA-512.
    const actual = await sha512File(outPath);
    if (expectedSha && actual.toLowerCase() !== expectedSha.toLowerCase()) {
      throw new Error('SHA512 mismatch. expected=' + expectedSha + ' actual=' + actual);
    }
    console.log('SHA512 OK: ' + actual);
    process.exit(0);
  } catch (e) {
    lastErr = e;
    console.error(`[attempt ${attempt}] failed: ${e.message}`);
    if (attempt < MAX_ATTEMPTS) {
      const delay = BASE_DELAY * Math.pow(2, attempt - 1);
      console.error(`  retry in ${delay}ms`);
      await sleep(delay);
    }
  }
}
console.error('GAVE UP after ' + MAX_ATTEMPTS + ' attempts: ' + (lastErr && lastErr.message));
process.exit(1);
